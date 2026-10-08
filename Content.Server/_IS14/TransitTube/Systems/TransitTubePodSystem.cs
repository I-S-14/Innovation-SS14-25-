using System.Numerics;
using Content.Server._IS14.TransitTube.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction.Completions;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared._IS14.TransitTube;
using Content.Shared._IS14.TransitTube.Components;
using Content.Shared._IS14.TransitTube.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._IS14.TransitTube.Systems;

/// <summary>
/// Moves transit tube pods through the tube network and handles who is riding inside them.
/// </summary>
public sealed class TransitTubePodSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TransitTubeStationSystem _station = default!;
    [Dependency] private readonly TransitTubeSystem _tube = default!;

    /// <summary>
    /// Floor on a single hop so that a chain of zero-delay sections cannot lock up the update loop.
    /// </summary>
    private const float MinimumHopTime = 0.05f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubePodComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TransitTubePodComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TransitTubePodComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TransitTubePodComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<TransitTubePodComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<TransitTubePodComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<TransitTubePodComponent, ConstructionBeforeDeleteEvent>(OnDeconstruct);
        SubscribeLocalEvent<TransitTubePodComponent, EntityTerminatingEvent>(OnTerminating);

        SubscribeLocalEvent<TransitTubeRiderComponent, MoveInputEvent>(OnRiderInput);
        SubscribeLocalEvent<TransitTubeRiderComponent, InhaleLocationEvent>(OnRiderInhale);
        SubscribeLocalEvent<TransitTubeRiderComponent, ExhaleLocationEvent>(OnRiderExhale);
        SubscribeLocalEvent<TransitTubeRiderComponent, AtmosExposedGetAirEvent>(OnRiderGetAir);
    }

    private void OnRiderInhale(Entity<TransitTubeRiderComponent> ent, ref InhaleLocationEvent args)
    {
        if (TryComp<TransitTubePodComponent>(ent.Comp.Pod, out var pod))
            args.Gas = pod.Air;
    }

    private void OnRiderExhale(Entity<TransitTubeRiderComponent> ent, ref ExhaleLocationEvent args)
    {
        if (TryComp<TransitTubePodComponent>(ent.Comp.Pod, out var pod))
            args.Gas = pod.Air;
    }

    private void OnRiderGetAir(Entity<TransitTubeRiderComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (!TryComp<TransitTubePodComponent>(ent.Comp.Pod, out var pod))
            return;

        args.Gas = pod.Air;
        args.Handled = true;
    }

    private void OnStartup(Entity<TransitTubePodComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, TransitTubePodComponent.ContainerId);
    }

    private void OnMapInit(Entity<TransitTubePodComponent> ent, ref MapInitEvent args)
    {
        FillWithAir(ent);
        TryDockToTile(ent);
    }

    /// <summary>
    /// Looks for a tube section under the pod and settles into it. This runs for a pod that
    /// was mapped in, built onto a station, or left loose when its station was taken apart,
    /// so building the line around an existing pod works as well as the other way round.
    /// </summary>
    private bool TryDockToTile(Entity<TransitTubePodComponent> ent)
    {
        var xform = Transform(ent);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return false;

        foreach (var entity in _map.GetLocal(xform.GridUid.Value, grid, xform.Coordinates))
        {
            if (!HasComp<TransitTubeComponent>(entity))
                continue;

            ent.Comp.CurrentTube = entity;
            _xform.SetCoordinates(ent, Transform(entity).Coordinates);
            _station.TryDock(entity, ent);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Taking a pod apart should not take its passengers with it.
    /// </summary>
    private void OnDeconstruct(EntityUid uid, TransitTubePodComponent component, ConstructionBeforeDeleteEvent args)
    {
        EmptyPod((uid, component));
    }

    /// <summary>
    /// Whatever takes the pod away — a tool, an explosion, an admin — the rider is put back
    /// on the floor instead of being deleted along with the shell around them. This runs
    /// before the pod's children are terminated, which is the last moment anything inside
    /// can still be got out.
    /// </summary>
    private void OnTerminating(Entity<TransitTubePodComponent> ent, ref EntityTerminatingEvent args)
    {
        var parent = Transform(ent).ParentUid;

        // The whole grid going away is not someone losing their ride: putting the rider
        // back on it would only have them deleted a moment later anyway.
        if (!parent.IsValid() || TerminatingOrDeleted(parent))
            return;

        // Forced, because by now the pod is past the point of being asked politely.
        _container.EmptyContainer(ent.Comp.Container, force: true, destination: Transform(ent).Coordinates);
    }

    private void OnShutdown(Entity<TransitTubePodComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.CurrentTube is { } tube)
            _station.ClearPod(tube, ent);
    }

    private void OnInsertAttempt(Entity<TransitTubePodComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (!HasComp<ItemComponent>(args.EntityUid) && !HasComp<BodyComponent>(args.EntityUid))
            args.Cancel();
    }

    private void OnInserted(Entity<TransitTubePodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        EnsureComp<TransitTubeRiderComponent>(args.Entity).Pod = ent;
        UpdateAppearance(ent);
    }

    private void OnRemoved(Entity<TransitTubePodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        RemComp<TransitTubeRiderComponent>(args.Entity);
        UpdateAppearance(ent);
    }

    /// <summary>
    /// A rider pressing a direction wants out. Only an actual key press counts: people board
    /// by running into a station, and the release of that very key would otherwise throw them
    /// straight back out again.
    /// </summary>
    private void OnRiderInput(Entity<TransitTubeRiderComponent> ent, ref MoveInputEvent args)
    {
        if (!args.State)
            return;

        if (TryComp<MobStateComponent>(ent, out var mobState) && !_mobState.IsAlive(ent.Owner, mobState))
            return;

        if (!TryComp<TransitTubePodComponent>(ent.Comp.Pod, out var pod) || pod.Moving)
            return;

        // At a station the doors have to be open first, so hand the request over to it.
        if (pod.CurrentTube is { } tube && HasComp<TransitTubeStationComponent>(tube))
        {
            _station.TryDisembark(tube, (ent.Comp.Pod, pod), ent);
            return;
        }

        _container.Remove(ent.Owner, pod.Container);
    }

    private void UpdateAppearance(Entity<TransitTubePodComponent> ent)
    {
        _appearance.SetData(ent, TransitTubePodVisuals.Occupied, ent.Comp.Container.ContainedEntities.Count > 0);
    }

    /// <summary>
    /// Gives a fresh pod a lungful of standard air.
    /// </summary>
    private void FillWithAir(Entity<TransitTubePodComponent> ent)
    {
        var air = ent.Comp.Air;
        if (air.TotalMoles > 0)
            return;

        var share = air.Volume / Atmospherics.CellVolume;
        air.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard * share);
        air.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard * share);
        air.Temperature = Atmospherics.T20C;
    }

    /// <summary>
    /// Swaps the pod's stale air for whatever the station is standing in. Called whenever a
    /// station finishes opening, which is the pod's only chance to breathe.
    /// </summary>
    public void VentPod(Entity<TransitTubePodComponent> ent)
    {
        if (_atmos.GetContainingMixture(ent.Owner, excite: true) is not { } environment)
            return;

        var air = ent.Comp.Air;

        // Everything goes out into the room, then the pod takes its own volume back,
        // which leaves both sides at the same pressure and temperature.
        _atmos.Merge(environment, air);
        air.Clear();

        var share = environment.RemoveRatio(air.Volume / (environment.Volume + air.Volume));
        _atmos.Merge(air, share);
    }

    /// <summary>
    /// Puts something inside the pod.
    /// </summary>
    public bool TryInsert(Entity<TransitTubePodComponent> ent, EntityUid toInsert)
    {
        return !ent.Comp.Moving && _container.Insert(toInsert, ent.Comp.Container);
    }

    /// <summary>
    /// Drops everything the pod carries onto its own tile.
    /// </summary>
    public void EmptyPod(Entity<TransitTubePodComponent> ent)
    {
        var contained = new List<EntityUid>(ent.Comp.Container.ContainedEntities);
        foreach (var entity in contained)
        {
            _container.Remove(entity, ent.Comp.Container);
        }
    }

    /// <summary>
    /// Sends the pod on its way out of <paramref name="fromTube"/>.
    /// </summary>
    public bool TryLaunch(Entity<TransitTubePodComponent> ent, EntityUid fromTube, Direction direction)
    {
        if (ent.Comp.Moving || direction == Direction.Invalid)
            return false;

        if (_tube.NextTubeFor(fromTube, direction) == null)
            return false;

        ent.Comp.CurrentTube = fromTube;
        ent.Comp.PreviousDirection = Direction.Invalid;
        ent.Comp.CurrentDirection = direction;
        ent.Comp.Moving = true;
        SetFacing(ent, direction);
        SetHopTime(ent, CompOrNull<TransitTubeComponent>(fromTube)?.ExitDelay ?? MinimumHopTime);
        _audio.PlayPvs(ent.Comp.LaunchSound, ent);

        return true;
    }

    /// <summary>
    /// Points the pod along a run of tube. Public because a pod can be put on the line
    /// without being launched, and a pod nobody has pointed anywhere faces south.
    /// </summary>
    public void SetFacing(Entity<TransitTubePodComponent> ent, Direction direction)
    {
        if (direction == Direction.Invalid)
            return;

        _xform.SetLocalRotation(ent, direction.ToAngle());
    }

    private void SetHopTime(Entity<TransitTubePodComponent> ent, float seconds)
    {
        ent.Comp.StartingTime = MathF.Max(seconds, MinimumHopTime);
        ent.Comp.TimeLeft = ent.Comp.StartingTime;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TransitTubePodComponent>();
        while (query.MoveNext(out var uid, out var pod))
        {
            if (pod.Moving)
            {
                UpdatePod((uid, pod), frameTime);
                continue;
            }

            if (pod.CurrentTube is { } tube && Exists(tube))
                continue;

            // The pod lost its section, or never had one. Keep an eye out for a new one.
            if (now < pod.NextDockCheck)
                continue;

            pod.NextDockCheck = now + TimeSpan.FromSeconds(1);
            pod.CurrentTube = null;
            TryDockToTile((uid, pod));
        }
    }

    private void UpdatePod(Entity<TransitTubePodComponent> ent, float frameTime)
    {
        while (frameTime > 0 && ent.Comp.Moving)
        {
            var time = MathF.Min(frameTime, ent.Comp.TimeLeft);
            ent.Comp.TimeLeft -= time;
            frameTime -= time;

            if (ent.Comp.CurrentTube is not { } tube || !Exists(tube))
            {
                EjectPod(ent);
                return;
            }

            if (ent.Comp.TimeLeft > 0)
            {
                // Slide towards the next tile so the ride is visible rather than a teleport.
                var progress = 1 - ent.Comp.TimeLeft / ent.Comp.StartingTime;
                var origin = Transform(tube).Coordinates;
                var step = ent.Comp.CurrentDirection.ToIntVec();
                _xform.SetCoordinates(ent, origin.Offset(new Vector2(step.X, step.Y) * progress));
                continue;
            }

            var next = _tube.NextTubeFor(tube, ent.Comp.CurrentDirection);
            if (next == null)
            {
                EjectPod(ent);
                return;
            }

            EnterTube(ent, next.Value);
        }
    }

    private void EnterTube(Entity<TransitTubePodComponent> ent, EntityUid tube)
    {
        ent.Comp.PreviousDirection = ent.Comp.CurrentDirection;
        ent.Comp.CurrentTube = tube;
        _xform.SetCoordinates(ent, Transform(tube).Coordinates);

        // Stations always catch the pod; they decide themselves when to send it on.
        if (TryComp<TransitTubeStationComponent>(tube, out var station))
        {
            ent.Comp.Moving = false;
            ent.Comp.TimeLeft = 0;
            _station.PodArrived((tube, station), ent);
            return;
        }

        var next = _tube.GetExit(tube, ent.Comp.CurrentDirection);
        if (next == Direction.Invalid)
        {
            EjectPod(ent);
            return;
        }

        ent.Comp.CurrentDirection = next;
        SetFacing(ent, next);

        var tubeComp = CompOrNull<TransitTubeComponent>(tube);
        SetHopTime(ent, (tubeComp?.EnterDelay ?? 0f) + (tubeComp?.ExitDelay ?? MinimumHopTime));
    }

    /// <summary>
    /// The tube ran out under a moving pod: throw the passengers clear and wreck the pod,
    /// the same way a disposal holder spills its contents at a broken pipe.
    /// </summary>
    public void EjectPod(Entity<TransitTubePodComponent> ent)
    {
        if (ent.Comp.Ejecting || Terminating(ent))
            return;

        ent.Comp.Ejecting = true;
        ent.Comp.Moving = false;

        if (ent.Comp.CurrentTube is { } tube)
            _station.ClearPod(tube, ent);

        ent.Comp.CurrentTube = null;

        var direction = ent.Comp.CurrentDirection != Direction.Invalid
            ? ent.Comp.CurrentDirection
            : ent.Comp.PreviousDirection;

        var contained = new List<EntityUid>(ent.Comp.Container.ContainedEntities);
        foreach (var entity in contained)
        {
            _container.Remove(entity, ent.Comp.Container, force: true);

            if (direction == Direction.Invalid)
                continue;

            var angle = direction.ToAngle() + _xform.GetWorldRotation(Transform(ent).ParentUid);
            _throwing.TryThrow(entity, angle.ToWorldVec() * 3f, 7f);
        }

        if (_atmos.GetContainingMixture(ent.Owner, excite: true) is { } environment)
        {
            _atmos.Merge(environment, ent.Comp.Air);
            ent.Comp.Air.Clear();
        }

        _popup.PopupEntity(Loc.GetString("is14-transit-tube-pod-wrecked"), ent, PopupType.MediumCaution);
        QueueDel(ent);
    }
}

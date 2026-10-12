using System.Linq;
using Content.Server.Popups;
using Content.Shared._IS14.TransitTube;
using Content.Shared._IS14.TransitTube.Systems;
using Content.Shared._IS14.TransitTube.Components;
using Content.Shared.Body.Components;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._IS14.TransitTube.Systems;

/// <summary>
/// Stations: the stops of the tube network. A station catches any pod that reaches it,
/// opens up so people can get in or out, and sends the pod on again once its dwell time
/// is up, which is what keeps pods circling the line on their own.
/// </summary>
public sealed class TransitTubeStationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TransitTubePodSystem _pod = default!;
    [Dependency] private readonly TransitTubeSystem _tube = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeStationComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TransitTubeStationComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<TransitTubeStationComponent, StartCollideEvent>(OnCollide);
    }

    private void OnStartup(Entity<TransitTubeStationComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnInteractHand(Entity<TransitTubeStationComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        switch (ent.Comp.State)
        {
            case TransitTubeStationState.Closed:
            case TransitTubeStationState.Closing:
                StartOpening(ent);
                break;

            case TransitTubeStationState.Open:
                // A loaded pod gets unloaded; otherwise the station hands you one to ride.
                if (ValidatePod(ent) is { } pod && pod.Comp.Container.ContainedEntities.Count > 0)
                {
                    _pod.EmptyPod(pod);
                    _popup.PopupEntity(Loc.GetString("is14-transit-tube-pod-unload"), ent, args.User);
                    break;
                }

                if (!TryBoard(ent, args.User, ignoreCooldown: true))
                    _popup.PopupEntity(Loc.GetString("is14-transit-tube-station-no-pod"), ent, args.User);

                break;
        }
    }

    /// <summary>
    /// Walking into an open station puts you in the pod, which is how anyone in a hurry
    /// is going to use these.
    /// </summary>
    private void OnCollide(Entity<TransitTubeStationComponent> ent, ref StartCollideEvent args)
    {
        TryBoard(ent, args.OtherEntity);
    }

    /// <summary>
    /// Puts a passer-by into the parked pod, if the station is open and the pod is free.
    /// </summary>
    public bool TryBoard(Entity<TransitTubeStationComponent> ent, EntityUid rider, bool ignoreCooldown = false)
    {
        if (ent.Comp.State != TransitTubeStationState.Open)
            return false;

        // Somebody who just got out is still standing in the doorway; let them leave.
        if (!ignoreCooldown && _timing.CurTime < ent.Comp.NextBoard)
            return false;

        // Only living things pile in by themselves; cargo has to be put in by hand.
        if (!HasComp<BodyComponent>(rider))
            return false;

        // No pod waiting? The station makes one around the passenger.
        var waiting = ValidatePod(ent);
        if ((waiting ?? Dispense(ent)) is not { } pod
            || pod.Comp.Container.ContainedEntities.Count > 0)
        {
            return false;
        }

        if (!_pod.TryInsert(pod, rider))
            return false;

        // No sense in making the passenger wait out the rest of the dwell they just walked in on.
        ent.Comp.NextLaunch = _timing.CurTime;
        _popup.PopupEntity(
            Loc.GetString(waiting == null ? "is14-transit-tube-pod-forms" : "is14-transit-tube-pod-enter"),
            ent,
            rider);

        return true;
    }

    /// <summary>
    /// Conjures a pod into the station for someone to ride. It docks itself on spawn.
    /// </summary>
    private Entity<TransitTubePodComponent>? Dispense(Entity<TransitTubeStationComponent> ent)
    {
        if (ent.Comp.Dispenses is not { } proto)
            return null;

        var uid = Spawn(proto, Transform(ent).Coordinates);
        if (!TryComp<TransitTubePodComponent>(uid, out var pod))
        {
            QueueDel(uid);
            return null;
        }

        pod.Temporary = true;

        // Nothing has launched this one yet, so it would sit facing south whichever way
        // the line runs. Point it the way the stop is about to send it.
        _pod.SetFacing((uid, pod), LaunchDirection(ent, pod));

        _audio.PlayPvs(ent.Comp.DispenseSound, ent);
        return (uid, pod);
    }

    /// <summary>
    /// Which way the station should fire its pod off: onwards if the pod was already
    /// going somewhere, back out the only opening if this is a terminus.
    /// </summary>
    private Direction LaunchDirection(Entity<TransitTubeStationComponent> ent, TransitTubePodComponent pod)
    {
        // Through traffic carries on the way it was going; everything else follows the
        // direction this stop prefers.
        if (!ent.Comp.Terminus
            && pod.CurrentDirection != Direction.Invalid
            && _tube.CanConnect(ent, pod.CurrentDirection))
        {
            return pod.CurrentDirection;
        }

        return _tube.PreferredDirection(ent);
    }

    /// <summary>
    /// Registers a pod that was mapped in on top of this section.
    /// </summary>
    public void TryDock(EntityUid tube, Entity<TransitTubePodComponent> pod)
    {
        if (!TryComp<TransitTubeStationComponent>(tube, out var station))
            return;

        station.Pod = pod;
        station.NextLaunch = _timing.CurTime + station.Dwell;
    }

    /// <summary>
    /// Forgets a pod that left, was wrecked or was deleted.
    /// </summary>
    public void ClearPod(EntityUid tube, EntityUid pod)
    {
        if (TryComp<TransitTubeStationComponent>(tube, out var station) && station.Pod == pod)
            station.Pod = null;
    }

    /// <summary>
    /// Called when a travelling pod reaches this station.
    /// </summary>
    public void PodArrived(Entity<TransitTubeStationComponent> ent, Entity<TransitTubePodComponent> pod)
    {
        ent.Comp.Pod = pod;
        ent.Comp.NextLaunch = _timing.CurTime + ent.Comp.Dwell;

        _audio.PlayPvs(ent.Comp.ArriveSound, ent);
        StartOpening(ent);
    }

    /// <summary>
    /// A rider pressed a movement key inside a parked pod: let them out, or knock on the
    /// doors if they are still shut.
    /// </summary>
    public void TryDisembark(EntityUid tube, Entity<TransitTubePodComponent> pod, EntityUid rider)
    {
        if (!TryComp<TransitTubeStationComponent>(tube, out var station))
            return;

        if (station.State == TransitTubeStationState.Open)
        {
            _container.Remove(rider, pod.Comp.Container);
            PutOnPlatform((tube, station), rider);
            station.NextBoard = _timing.CurTime + station.BoardCooldown;
            return;
        }

        StartOpening((tube, station));
    }

    /// <summary>
    /// Steps someone who just left the pod off the station itself and onto the floor beside
    /// it, so they are not left standing inside a solid structure.
    /// </summary>
    private void PutOnPlatform(Entity<TransitTubeStationComponent> ent, EntityUid rider)
    {
        var xform = Transform(ent);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var facing = xform.LocalRotation.GetDir();
        var indices = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

        foreach (var direction in new[] { facing, facing.GetOpposite() })
        {
            var target = SharedMapSystem.GetDirection(indices, direction);
            if (!_map.TryGetTileRef(xform.GridUid.Value, grid, target, out var tile) || tile.Tile.IsEmpty)
                continue;

            if (_turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            _xform.SetCoordinates(rider, _map.ToCoordinates(xform.GridUid.Value, target, grid));
            return;
        }
    }

    private void StartOpening(Entity<TransitTubeStationComponent> ent)
    {
        if (ent.Comp.State is TransitTubeStationState.Open or TransitTubeStationState.Opening)
            return;

        // Opening always buys the full dwell time, so a station opened by hand does not
        // slam shut again on the next tick.
        ent.Comp.NextLaunch = _timing.CurTime + ent.Comp.Dwell;
        SetState(ent, TransitTubeStationState.Opening, ent.Comp.OpenTime);
        _audio.PlayPvs(ent.Comp.DoorSound, ent);
    }

    private void StartClosing(Entity<TransitTubeStationComponent> ent)
    {
        if (ent.Comp.State is TransitTubeStationState.Closed or TransitTubeStationState.Closing)
            return;

        SetState(ent, TransitTubeStationState.Closing, ent.Comp.CloseTime);
        _audio.PlayPvs(ent.Comp.DoorSound, ent);
    }

    private void SetState(Entity<TransitTubeStationComponent> ent, TransitTubeStationState state, float duration = 0f)
    {
        ent.Comp.State = state;
        ent.Comp.StateEnd = _timing.CurTime + TimeSpan.FromSeconds(duration);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<TransitTubeStationComponent> ent)
    {
        _appearance.SetData(ent, TransitTubeStationVisuals.State, ent.Comp.State);
    }

    /// <summary>
    /// The pod this station is holding, if it is still real and still here.
    /// </summary>
    private Entity<TransitTubePodComponent>? ValidatePod(Entity<TransitTubeStationComponent> ent)
    {
        if (ent.Comp.Pod is not { } pod
            || !TryComp<TransitTubePodComponent>(pod, out var podComp)
            || podComp.CurrentTube != ent.Owner)
        {
            ent.Comp.Pod = null;
            return null;
        }

        return (pod, podComp);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TransitTubeStationComponent>();

        while (query.MoveNext(out var uid, out var station))
        {
            var ent = new Entity<TransitTubeStationComponent>(uid, station);

            // Finish whatever the doors were doing.
            switch (station.State)
            {
                case TransitTubeStationState.Opening when now >= station.StateEnd:
                    SetState(ent, TransitTubeStationState.Open);

                    // Open doors are the pod's only chance to swap its air for the room's.
                    if (ValidatePod(ent) is { } docked)
                        _pod.VentPod(docked);

                    break;

                case TransitTubeStationState.Closing when now >= station.StateEnd:
                    SetState(ent, TransitTubeStationState.Closed);
                    break;
            }

            var pod = ValidatePod(ent);
            if (pod == null)
            {
                // A station with nothing to send waits with its doors open, so that walking
                // into one is all it takes to get a pod.
                if (station.State == TransitTubeStationState.Closed)
                    StartOpening(ent);

                continue;
            }

            if (pod.Value.Comp.Moving)
                continue;

            var empty = pod.Value.Comp.Container.ContainedEntities.Count == 0;

            // A pod the station made for a passenger is of no use once they have stepped out.
            if (empty && pod.Value.Comp.Temporary)
            {
                _audio.PlayPvs(station.DissolveSound, ent);
                QueueDel(pod.Value.Owner);
                station.Pod = null;
                continue;
            }

            // An empty pod stays put rather than touring the line on its own. The next
            // passenger gets this one instead of a freshly made pod.
            if (empty)
            {
                if (station.State == TransitTubeStationState.Closed)
                    StartOpening(ent);

                continue;
            }

            if (now < station.NextLaunch)
            {
                if (station.State == TransitTubeStationState.Closed)
                    StartOpening(ent);

                continue;
            }

            switch (station.State)
            {
                case TransitTubeStationState.Open:
                    StartClosing(ent);
                    break;

                case TransitTubeStationState.Closed:
                    Launch(ent, pod.Value);
                    break;
            }
        }
    }

    private void Launch(Entity<TransitTubeStationComponent> ent, Entity<TransitTubePodComponent> pod)
    {
        var direction = LaunchDirection(ent, pod.Comp);

        // A dead end, a tube that was cut, or simply nowhere to go: hold the pod and try later.
        if (direction == Direction.Invalid || !_pod.TryLaunch(pod, ent, direction))
        {
            ent.Comp.NextLaunch = _timing.CurTime + ent.Comp.Dwell;
            return;
        }

        ent.Comp.Pod = null;
    }
}

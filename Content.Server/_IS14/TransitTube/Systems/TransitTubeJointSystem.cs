using Content.Shared._IS14.TransitTube;
using Content.Shared._IS14.TransitTube.Components;
using Content.Shared._IS14.TransitTube.Systems;
using Robust.Shared.Map.Components;

namespace Content.Server._IS14.TransitTube.Systems;

/// <summary>
/// Keeps track of which ends of a section actually meet another one, so the client can
/// dress those ends and leave the rest bare. Without it a lone diagonal would sit there
/// with the corner pieces of a joint it does not have.
/// </summary>
public sealed class TransitTubeJointSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TransitTubeSystem _tube = default!;

    /// <summary>
    /// Sections whose joints need working out again, done at the end of the tick because a
    /// section that is being removed is still there while its neighbours are told about it.
    /// </summary>
    private readonly HashSet<EntityUid> _stale = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TransitTubeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TransitTubeComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<TransitTubeComponent, MoveEvent>(OnMoved);
    }

    private void OnStartup(Entity<TransitTubeComponent> ent, ref ComponentStartup args)
    {
        MarkStale(ent);
    }

    private void OnShutdown(Entity<TransitTubeComponent> ent, ref ComponentShutdown args)
    {
        MarkStale(ent);
    }

    private void OnAnchorChanged(Entity<TransitTubeComponent> ent, ref AnchorStateChangedEvent args)
    {
        MarkStale(ent);
    }

    private void OnMoved(Entity<TransitTubeComponent> ent, ref MoveEvent args)
    {
        // Turning a section with a tool changes which ends it offers.
        MarkStale(ent);
    }

    /// <summary>
    /// Queues this section and everything around it, since a joint is a matter between two
    /// sections and both sides draw their own half of it. Public because a tool can change
    /// which ends a section offers without moving it at all.
    /// </summary>
    public void MarkStale(EntityUid uid)
    {
        _stale.Add(uid);

        var xform = Transform(uid);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        foreach (var direction in DirectionExtensions.AllDirections)
        {
            foreach (var neighbour in _map.GetInDir(xform.GridUid.Value, grid, xform.Coordinates, direction))
            {
                if (HasComp<TransitTubeComponent>(neighbour))
                    _stale.Add(neighbour);
            }
        }
    }

    public override void Update(float frameTime)
    {
        if (_stale.Count == 0)
            return;

        foreach (var uid in _stale)
        {
            if (!Exists(uid) || Terminating(uid) || !HasComp<TransitTubeComponent>(uid))
                continue;

            var connections = 0;
            foreach (var direction in _tube.GetConnectableDirections(uid))
            {
                if (_tube.NextTubeFor(uid, direction) != null)
                    connections |= 1 << (int) direction;
            }

            _appearance.SetData(uid, TransitTubeVisuals.Connections, connections);

            if (TryComp<TransitTubeJunctionComponent>(uid, out var junction))
                _appearance.SetData(uid, TransitTubeVisuals.Flipped, junction.PreferCounterClockwise);

            // Kept in step with the component so a bend does not fall back to the plain
            // picture the first time anything else touches this section's looks.
            if (TryComp<TransitTubeBendComponent>(uid, out var bend))
                _appearance.SetData(uid, TransitTubeVisuals.Diagonal, bend.Diagonal);
        }

        _stale.Clear();
    }
}

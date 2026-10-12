using System.Linq;
using Content.Shared._IS14.TransitTube.Events;
using Content.Shared._IS14.TransitTube.Components;
using Robust.Shared.Map.Components;

namespace Content.Shared._IS14.TransitTube.Systems;

/// <summary>
/// Shape logic of the tube network: which way a section connects and which section
/// comes next. Pod movement itself lives in <see cref="TransitTubePodSystem"/>.
/// </summary>
public sealed class TransitTubeSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeStraightComponent, GetTransitTubeConnectableDirectionsEvent>(OnStraightConnectable);
        SubscribeLocalEvent<TransitTubeBendComponent, GetTransitTubeConnectableDirectionsEvent>(OnBendConnectable);
        SubscribeLocalEvent<TransitTubeJunctionComponent, GetTransitTubeConnectableDirectionsEvent>(OnJunctionConnectable);
        SubscribeLocalEvent<TransitTubeStationComponent, GetTransitTubeConnectableDirectionsEvent>(OnStationConnectable);
    }

    /// <summary>
    /// Turns a direction by <paramref name="steps"/> eighths of a circle.
    /// </summary>
    public static Direction Turn(Direction direction, int steps)
    {
        return (Direction) (((int) direction + steps + 8) % 8);
    }

    /// <summary>
    /// Every direction the given section can be entered from or left towards.
    /// </summary>
    public Direction[] GetConnectableDirections(EntityUid tube)
    {
        var ev = new GetTransitTubeConnectableDirectionsEvent { Connectable = Array.Empty<Direction>() };
        RaiseLocalEvent(tube, ref ev);
        return ev.Connectable;
    }

    public bool CanConnect(EntityUid tube, Direction direction)
    {
        return GetConnectableDirections(tube).Contains(direction);
    }

    /// <summary>
    /// Where a pod that entered <paramref name="tube"/> heading <paramref name="inDirection"/>
    /// leaves again: straight on if the section allows it, otherwise the nearest end within
    /// 45 degrees. <see cref="Direction.Invalid"/> means the pod is stuck and gets thrown out.
    /// </summary>
    public Direction GetExit(EntityUid tube, Direction inDirection)
    {
        if (inDirection == Direction.Invalid)
            return Direction.Invalid;

        var clockwise = Turn(inDirection, -1);
        var counterClockwise = Turn(inDirection, 1);
        var near = Direction.Invalid;

        foreach (var direction in GetConnectableDirections(tube))
        {
            if (direction == inDirection)
                return direction;

            // Later entries win, which is how a section states its preferred branch.
            if (direction == clockwise || direction == counterClockwise)
                near = direction;
        }

        return near;
    }

    /// <summary>
    /// The section a pod leaving <paramref name="tube"/> in <paramref name="direction"/> ends up in,
    /// or null if nothing over there accepts it.
    /// </summary>
    public EntityUid? NextTubeFor(EntityUid tube, Direction direction)
    {
        if (direction == Direction.Invalid || !HasComp<TransitTubeComponent>(tube))
            return null;

        if (!CanConnect(tube, direction))
            return null;

        var xform = Transform(tube);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var opposite = direction.GetOpposite();

        foreach (var entity in _map.GetInDir(xform.GridUid.Value, grid, xform.Coordinates, direction))
        {
            if (!HasComp<TransitTubeComponent>(entity))
                continue;

            if (!CanConnect(entity, opposite))
                continue;

            return entity;
        }

        return null;
    }

    /// <summary>
    /// Where a station sends a pod that has nowhere particular to be: the first direction
    /// it connects to, or the other one if the stop is set to prefer it.
    /// </summary>
    public Direction PreferredDirection(Entity<TransitTubeStationComponent> ent)
    {
        var connectable = GetConnectableDirections(ent);
        if (connectable.Length == 0)
            return Direction.Invalid;

        var index = ent.Comp.PreferCounterClockwise && connectable.Length > 1 ? 1 : 0;
        return connectable[index];
    }

    private void OnStationConnectable(Entity<TransitTubeStationComponent> ent, ref GetTransitTubeConnectableDirectionsEvent args)
    {
        // The station faces the corridor it serves, so the tube itself runs across it.
        var rotation = Transform(ent).LocalRotation;
        var clockwise = new Angle(rotation.Theta - Math.PI / 2).GetDir();
        var counterClockwise = new Angle(rotation.Theta + Math.PI / 2).GetDir();

        args.Connectable = ent.Comp.Terminus
            ? new[] { clockwise }
            : new[] { clockwise, counterClockwise };
    }

    private void OnStraightConnectable(Entity<TransitTubeStraightComponent> ent, ref GetTransitTubeConnectableDirectionsEvent args)
    {
        // Each quarter turn moves the run on by an eighth, so the four turns give four
        // different lines rather than two of them twice over.
        var turn = (int) Transform(ent).LocalRotation.GetDir() / 2;
        var axis = Turn(Direction.South, turn);

        args.Connectable = new[] { axis, axis.GetOpposite() };
    }

    /// <summary>
    /// Which way a section looks: the way it is turned, or 45 degrees round from it for the
    /// diagonal half of a shape's pair.
    /// </summary>
    private Direction Facing(EntityUid uid, bool diagonal)
    {
        var forward = Transform(uid).LocalRotation.GetDir();

        return diagonal ? Turn(forward, 1) : forward;
    }

    private void OnBendConnectable(Entity<TransitTubeBendComponent> ent, ref GetTransitTubeConnectableDirectionsEvent args)
    {
        // 135 degrees apart, which is what a 45 degree turn between a straight run and a
        // diagonal one comes to.
        var forward = Facing(ent, ent.Comp.Diagonal);

        args.Connectable = new[] { forward, Turn(forward, 3) };
    }

    private void OnJunctionConnectable(Entity<TransitTubeJunctionComponent> ent, ref GetTransitTubeConnectableDirectionsEvent args)
    {
        var forward = Transform(ent).LocalRotation.GetDir();
        var back = forward.GetOpposite();
        var first = Turn(back, ent.Comp.PreferCounterClockwise ? -1 : 1);
        var second = Turn(back, ent.Comp.PreferCounterClockwise ? 1 : -1);

        // The last branch is the preferred one, see GetExit.
        args.Connectable = new[] { forward, first, second };
    }
}

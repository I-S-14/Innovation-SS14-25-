// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Content.Shared._IS14.Overlays;
using Content.Shared.Doors.Components;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._IS14.Overlays;

/// <summary>
///     Paints a floor plan over the viewport: deck plating, then walls, then doors.
/// </summary>
public sealed class StructuralVisionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _maps;
    private readonly OccluderSystem _occluder;
    private readonly SharedTransformSystem _transform;
    private readonly TurfSystem _turf;

    // Not readonly: FindGridsIntersecting takes the list by ref so it can grow it.
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<Entity<OccluderComponent, TransformComponent>> _occluders = new();
    private readonly HashSet<Entity<DoorComponent>> _doors = new();

    /// <summary>
    ///     Fraction of the range at which the fade starts. A hard edge on a circle still
    ///     reads as a rendering fault; a fade reads as the limit of an instrument.
    /// </summary>
    private const float FadeStart = 0.75f;

    /// <summary>
    ///     Live settings, pushed by <see cref="StructuralVisionSystem"/> from whatever the
    ///     wearer has on. Never null so the overlay can be constructed before anything is
    ///     equipped and simply draw the defaults if it ever runs early.
    /// </summary>
    public StructuralVisionComponent Settings = new();

    /// <summary>
    ///     The whole trick, in one line. Clyde renders WorldSpace overlays after
    ///     ApplyFovToBuffer (Clyde.HLR.cs), so this is the one space that is not cut by the
    ///     FOV mask. WorldSpaceBelowFOV — the obvious-looking choice — is drawn just before
    ///     it and would be masked away exactly like the rest of the world.
    /// </summary>
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public StructuralVisionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _lookup = _entity.System<EntityLookupSystem>();
        _maps = _entity.System<SharedMapSystem>();
        _occluder = _entity.System<OccluderSystem>();
        _transform = _entity.System<SharedTransformSystem>();
        _turf = _entity.System<TurfSystem>();
    }

    /// <summary>
    ///     Every viewport runs the same overlay list, security cameras included. A floor
    ///     plan drawn around a camera the wearer is watching is nonsense, so only the eye
    ///     the player is actually looking through gets one.
    /// </summary>
    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eye.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var range = Settings.Range;

        if (range <= 0f
            || _player.LocalEntity is not { } player
            || !_entity.TryGetComponent(player, out TransformComponent? xform)
            || xform.MapID != args.MapId)
        {
            return;
        }

        var origin = _transform.GetWorldPosition(xform);

        // The scan is a circle, but everything that queries the world wants a box. Start
        // from the square around it, clipped to what is on screen, and throw the corners
        // away per shape as they are drawn.
        var bounds = Box2.CenteredAround(origin, new Vector2(range * 2f, range * 2f))
            .Intersect(args.WorldAABB);

        if (bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        var handle = args.WorldHandle;

        // Order is the layering: plating underneath, walls over it, doors last so a door
        // that is also an occluder ends up in the door colour rather than the wall one.
        DrawFloors(handle, args.MapId, bounds, origin, range);
        DrawWalls(handle, args.MapId, bounds, origin, range);
        DrawDoors(handle, args.MapId, bounds, origin, range);

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawFloors(DrawingHandleWorld handle, MapId mapId, Box2 bounds, Vector2 origin, float range)
    {
        _grids.Clear();
        _map.FindGridsIntersecting(mapId, bounds, ref _grids);

        foreach (var grid in _grids)
        {
            var matrix = _transform.GetWorldMatrix(grid.Owner);
            handle.SetTransform(matrix);

            var tiles = _maps.GetTilesEnumerator(grid.Owner, grid.Comp, bounds);

            while (tiles.MoveNext(out var tile))
            {
                // Space is not plating. Without this the scanner draws a solid slab over
                // the void around the station and the outline of the hull disappears.
                if (_turf.IsSpace(tile))
                    continue;

                var local = _lookup.GetLocalBounds(tile, grid.Comp.TileSize);

                if (!TryFade(Vector2.Transform(local.Center, matrix), origin, range, Settings.FloorColor, out var color))
                    continue;

                handle.DrawRect(local, color);
            }
        }
    }

    private void DrawWalls(DrawingHandleWorld handle, MapId mapId, Box2 bounds, Vector2 origin, float range)
    {
        _occluders.Clear();
        _occluder.QueryAabb(_occluders, mapId, bounds);

        foreach (var occluder in _occluders)
        {
            var matrix = _transform.GetWorldMatrix(occluder.Comp2);

            if (!TryFade(Vector2.Transform(Vector2.Zero, matrix), origin, range, Settings.WallColor, out var color))
                continue;

            handle.SetTransform(matrix);
            handle.DrawRect(Box2.UnitCentered, color);
        }
    }

    private void DrawDoors(DrawingHandleWorld handle, MapId mapId, Box2 bounds, Vector2 origin, float range)
    {
        _doors.Clear();
        _lookup.GetEntitiesIntersecting(mapId, bounds, _doors);

        foreach (var door in _doors)
        {
            if (!_entity.TryGetComponent(door.Owner, out TransformComponent? xform))
                continue;

            var matrix = _transform.GetWorldMatrix(xform);

            if (!TryFade(Vector2.Transform(Vector2.Zero, matrix), origin, range, Settings.DoorColor, out var color))
                continue;

            handle.SetTransform(matrix);
            handle.DrawRect(Box2.UnitCentered, color);
        }
    }

    /// <summary>
    ///     Rejects anything outside the scan circle and dims what is near its edge.
    /// </summary>
    private static bool TryFade(Vector2 point, Vector2 origin, float range, Color color, out Color faded)
    {
        faded = color;

        var distance = (point - origin).Length();

        if (distance > range)
            return false;

        var fade = range * FadeStart;

        if (distance <= fade)
            return true;

        faded = color.WithAlpha(color.A * (1f - (distance - fade) / (range - fade)));
        return true;
    }
}

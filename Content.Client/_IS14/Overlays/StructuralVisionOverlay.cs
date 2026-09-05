// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Content.Shared._IS14.Overlays;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._IS14.Overlays;

/// <summary>
///     Redraws the station's layout — floor tiles, and whatever carries
///     StructuralVisionTargetComponent, with their real sprites — inside the area the field
///     of view has blacked out.
/// </summary>
public sealed class StructuralVisionOverlay : Overlay
{
    /// <summary>
    ///     Draws only where the stencil buffer holds 1. That is exactly the region the FOV
    ///     pass blacked out: fov.swsl discards on every fragment it does not occlude, so
    ///     ApplyFovToBuffer (Clyde.LightRendering.cs) leaves a 1 behind only in shadow.
    ///     Without it the redraw would also land on top of what the player can already see,
    ///     covering mobs, items and decals with a flat unlit copy of the floor.
    /// </summary>
    private static readonly ProtoId<ShaderPrototype> StencilDraw = "StencilEqualDraw";

    /// <summary>
    ///     Fades the redraw back to the FOV colour along the edge of the field of view. See
    ///     structural_vision_feather.swsl for how the edge is found.
    /// </summary>
    private static readonly ProtoId<ShaderPrototype> FeatherShader = "IS14StructuralVisionFeather";

    /// <summary>
    ///     Below every world-space overlay content ships, so none of them get painted over.
    /// </summary>
    private const int LayoutZIndex = -100;

    /// <summary>
    ///     Tile sprite sheets are a horizontal strip of <see cref="ContentTileDefinition.Variants"/>
    ///     frames, each one tile square at this many pixels. Same constant the engine's
    ///     atlas builder uses; it is not going to change without the whole renderer changing.
    /// </summary>
    private const int TilePixels = EyeManager.PixelsPerMeter;

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _resource = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _maps;
    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _transform;
    private readonly TurfSystem _turf;

    // Not readonly: FindGridsIntersecting takes the list by ref so it can grow it.
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly HashSet<Entity<StructuralVisionTargetComponent>> _targets = new();
    private readonly HashSet<Entity<IComponent>> _extra = new();

    /// <summary>
    ///     Guards against drawing the same entity twice when it turns up in more than one
    ///     source — a revealed pipe running under a grille, for instance.
    /// </summary>
    private readonly HashSet<EntityUid> _seen = new();

    /// <summary>
    ///     Everything the structure pass is about to draw, so it can be put in order first.
    ///     A lookup hands entities back in whatever order its trees happen to hold them, and
    ///     drawing in that order puts a grille over the window that is standing in front of
    ///     it. Reused every frame rather than reallocated.
    /// </summary>
    private readonly List<Structure> _structures = new();

    /// <summary>
    ///     Tile sprites by tile type id. The resource cache already caches the textures
    ///     themselves; this just keeps a per-tile ResPath lookup out of a loop that runs
    ///     several hundred times a frame.
    /// </summary>
    private readonly Dictionary<int, Texture?> _tileTextures = new();

    /// <summary>
    ///     Its own instance because it carries per-frame parameters, and built once because
    ///     InstanceUnique duplicates a compiled shader. Resolved on first draw rather than in
    ///     the constructor: the overlay is built from the system's Initialize, which is not a
    ///     point where prototypes are guaranteed to be loaded yet.
    /// </summary>
    private ShaderInstance? _feather;

    /// <summary>
    ///     Live settings, pushed by <see cref="StructuralVisionSystem"/> from whatever the
    ///     wearer has on. Never null so the overlay can be constructed before anything is
    ///     equipped.
    /// </summary>
    public StructuralVisionComponent Settings = new();

    /// <summary>
    ///     Clyde renders WorldSpace overlays after ApplyFovToBuffer (Clyde.HLR.cs), so this
    ///     is the one space that is not cut by the FOV mask, and the one place the stencil
    ///     the FOV pass wrote is still there to read. WorldSpaceBelowFOV — the
    ///     obvious-looking choice — is drawn just before it and would be masked away
    ///     exactly like the rest of the world.
    /// </summary>
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    /// <summary>
    ///     Asks Clyde for a copy of the viewport taken just before this overlay draws — the
    ///     FOV is already on it and nothing of ours is yet, which is exactly the image the
    ///     feather pass needs to find the edge of the shadow in.
    /// </summary>
    public override bool RequestScreenTexture => true;

    /// <summary>
    ///     Extra component types to redraw alongside the layout, in the order given. This is
    ///     how another scanner gets to work through walls: anything it has already marked on
    ///     the client — TRayRevealed on a revealed pipe, say — can be listed here and the
    ///     stencil pass picks it up. Managed by <see cref="StructuralVisionSystem"/>.
    /// </summary>
    public readonly List<Type> ExtraSources = new();

    public StructuralVisionOverlay()
    {
        IoCManager.InjectDependencies(this);

        // Everything the scanner draws is a background for the real render, and other
        // world-space overlays are the real render's business. Overlays sharing a ZIndex are
        // drawn in an arbitrary order (OverlayManager), so leaving this at the default meant
        // the redrawn floor sometimes landed on top of the mining scanner's ore markers, and
        // sometimes did not, depending on what had been equipped first.
        ZIndex = LayoutZIndex;

        _lookup = _entity.System<EntityLookupSystem>();
        _maps = _entity.System<SharedMapSystem>();
        _sprites = _entity.System<SpriteSystem>();
        _transform = _entity.System<SharedTransformSystem>();
        _turf = _entity.System<TurfSystem>();

        _proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    protected override void DisposeBehavior()
    {
        _proto.PrototypesReloaded -= OnPrototypesReloaded;

        base.DisposeBehavior();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _tileTextures.Clear();

        // Dropped rather than rebuilt here, so a shader edited under hot reload is picked up
        // on the next frame that actually needs it.
        _feather = null;
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
        var shader = _proto.Index(StencilDraw).Instance();
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        handle.UseShader(shader);

        DrawFloors(handle, args.MapId, bounds, origin, range);
        DrawStructures(handle, shader, args.MapId, bounds, origin, range, eyeRotation);

        handle.SetTransform(Matrix3x2.Identity);

        DrawFeather(handle, bounds);

        handle.UseShader(null);
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
                // Space is not plating. Without this the scanner paints the void around the
                // station and the outline of the hull disappears.
                if (_turf.IsSpace(tile))
                    continue;

                if (GetTileTexture(tile.Tile.TypeId) is not { } texture)
                    continue;

                var local = _lookup.GetLocalBounds(tile, grid.Comp.TileSize);

                if (!InRange(Vector2.Transform(local.Center, matrix), origin, range))
                    continue;

                // Variants sit side by side in one strip. A tile that claims a variant the
                // sheet does not have falls back to the first, same as the engine's atlas.
                var variant = tile.Tile.Variant * TilePixels < texture.Width ? tile.Tile.Variant : 0;
                var region = UIBox2.FromDimensions(variant * TilePixels, 0, TilePixels, TilePixels);

                handle.DrawTextureRectRegion(texture, local, Settings.Tint, region);
            }
        }
    }

    /// <summary>
    ///     Structure, drawn with its own sprites. One source and one only: whatever carries
    ///     <see cref="StructuralVisionTargetComponent"/>.
    ///
    ///     This used to guess, by asking the engine which entities block light. The guess was
    ///     wrong in both directions — it pulled in bookshelves, curtains and closed airlocks,
    ///     and it could not see a grille at all, because a grille blocks nothing. A list you
    ///     can read is worth more here than a rule that is nearly right.
    /// </summary>
    private void DrawStructures(
        DrawingHandleWorld handle,
        ShaderInstance shader,
        MapId mapId,
        Box2 bounds,
        Vector2 origin,
        float range,
        Angle eyeRotation)
    {
        _structures.Clear();
        _seen.Clear();

        _targets.Clear();
        _lookup.GetEntitiesIntersecting(mapId, bounds, _targets);

        foreach (var target in _targets)
        {
            Collect(target.Owner, origin, range, eyeRotation);
        }

        foreach (var source in ExtraSources)
        {
            _extra.Clear();
            _lookup.GetEntitiesIntersecting(source, mapId, bounds, _extra);

            foreach (var entity in _extra)
            {
                Collect(entity.Owner, origin, range, eyeRotation);
            }
        }

        _structures.Sort(CompareDrawOrder);

        foreach (var structure in _structures)
        {
            DrawEntity(handle, shader, structure, eyeRotation);
        }
    }

    private void Collect(EntityUid uid, Vector2 origin, float range, Angle eyeRotation)
    {
        if (!_seen.Add(uid)
            || !_entity.TryGetComponent(uid, out TransformComponent? xform)
            || !_entity.TryGetComponent(uid, out SpriteComponent? sprite)
            || !sprite.Visible)
        {
            return;
        }

        var position = _transform.GetWorldPosition(xform);

        if (!InRange(position, origin, range))
            return;

        // Sort key for the y-pass, in eye space rather than world space: what counts is which
        // sprite is lower on screen, and with the eye rotated those are not the same axis.
        // The eye's own position drops out, it shifts every key alike.
        _structures.Add(new Structure(uid, sprite, xform, position, eyeRotation.RotateVec(position).Y));
    }

    /// <summary>
    ///     The engine's sprite order, reproduced: draw depth first, then render order, then
    ///     bottom-most on screen last, then the entity id so the result is at least stable.
    ///     See SpriteDrawingOrderComparer in Clyde.Sprite.cs.
    ///
    ///     Without this the scanner disagrees with the normal render about which sprite is on
    ///     top — most visibly on a window reinforced with a grille, where the two sit on the
    ///     same tile and only the draw depth separates them.
    ///
    ///     The y-pass compares entity positions where the engine compares screen bounding
    ///     boxes. Cheaper, and it only parts ways for sprites of differing height whose
    ///     positions tie, which anchored structure does not do.
    /// </summary>
    private static int CompareDrawOrder(Structure a, Structure b)
    {
        var cmp = a.Sprite.DrawDepth.CompareTo(b.Sprite.DrawDepth);

        if (cmp != 0)
            return cmp;

        cmp = a.Sprite.RenderOrder.CompareTo(b.Sprite.RenderOrder);

        if (cmp != 0)
            return cmp;

        // Descending: further up the screen is further away, and goes down first.
        cmp = b.SortDepth.CompareTo(a.SortDepth);

        return cmp != 0 ? cmp : a.Uid.CompareTo(b.Uid);
    }

    private readonly record struct Structure(
        EntityUid Uid,
        SpriteComponent Sprite,
        TransformComponent Xform,
        Vector2 Position,
        float SortDepth);

    private void DrawEntity(DrawingHandleWorld handle, ShaderInstance shader, Structure structure, Angle eyeRotation)
    {
        var (uid, sprite, xform, position, _) = structure;

        // RenderSprite takes no modulate, so the tint goes on the sprite itself and comes
        // straight back off. SetColor only assigns the field — nothing is dirtied, nothing
        // is networked — and the entity is drawn before anything else can read it.
        var original = sprite.Color;
        _sprites.SetColor((uid, sprite), original * Settings.Tint);

        _sprites.RenderSprite((uid, sprite), handle, eyeRotation, _transform.GetWorldRotation(xform), position);

        _sprites.SetColor((uid, sprite), original);

        // A layer carrying its own shader resets the handle to the default one when it is
        // done (SpriteSystem.Render.cs), which would drop the stencil for everything drawn
        // after it. Cheaper to put it back than to find out which sprites do that.
        handle.UseShader(shader);
    }

    /// <summary>
    ///     Paints the FOV colour back over the redraw near the edge of the field of view, so
    ///     it fades out instead of stopping on a line. Runs last: it has to sit on top of
    ///     everything the scanner drew, and it reads the screen as it was before any of it.
    /// </summary>
    private void DrawFeather(DrawingHandleWorld handle, Box2 bounds)
    {
        if (Settings.Feather <= 0f || ScreenTexture == null)
            return;

        if (!Color.TryParse(_cfg.GetCVar(CVars.RenderFOVColor), out var fovColor))
            fovColor = Color.Black;

        _feather ??= _proto.Index(FeatherShader).InstanceUnique();

        _feather.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _feather.SetParameter("fovColor", fovColor);
        _feather.SetParameter("featherWidth", Settings.Feather);

        // Only over what the scanner could have drawn on. Everywhere else in shadow the
        // pass would be painting the FOV colour onto the FOV colour, at sixteen texture
        // samples a pixel for nothing.
        handle.UseShader(_feather);
        handle.DrawRect(bounds, Color.White);
    }

    /// <summary>
    ///     Tile sprite sheet for a tile type, or null for tiles that have no sprite at all.
    /// </summary>
    private Texture? GetTileTexture(int typeId)
    {
        if (_tileTextures.TryGetValue(typeId, out var cached))
            return cached;

        Texture? texture = null;

        if (_tileDefs[typeId] is ContentTileDefinition { Sprite: { } path }
            && _resource.TryGetResource<TextureResource>(path, out var resource))
        {
            texture = resource.Texture;
        }

        _tileTextures[typeId] = texture;
        return texture;
    }

    private static bool InRange(Vector2 point, Vector2 origin, float range)
    {
        return (point - origin).LengthSquared() <= range * range;
    }
}

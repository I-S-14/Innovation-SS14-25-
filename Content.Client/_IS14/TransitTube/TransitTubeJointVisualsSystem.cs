using Content.Shared._IS14.TransitTube;
using Content.Shared._IS14.TransitTube.Components;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.TransitTube;

/// <summary>
/// Draws a section: the shape itself, and the pieces that dress an end only where that end
/// actually meets another section, the way smoothed walls pick their sprite from their
/// neighbours. The layers all come from the one base prototype, so the shape is picked here
/// rather than by each prototype spelling out its own layers.
/// </summary>
public sealed class TransitTubeJointVisualsSystem : VisualizerSystem<TransitTubeComponent>
{
    /// <summary>
    /// The ends that carry a joint piece. A diagonal joint is dressed by the section on
    /// its northward side, so the two southward diagonals have nothing to draw.
    /// </summary>
    private static readonly (Direction Direction, TransitTubeJointLayers Layer)[] Joints =
    {
        (Direction.North, TransitTubeJointLayers.North),
        (Direction.South, TransitTubeJointLayers.South),
        (Direction.East, TransitTubeJointLayers.East),
        (Direction.West, TransitTubeJointLayers.West),
        (Direction.NorthEast, TransitTubeJointLayers.NorthEast),
        (Direction.NorthWest, TransitTubeJointLayers.NorthWest),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<TransitTubeComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            SetShape(ent, sprite, IsDiagonal(ent));
    }

    /// <summary>
    /// Which half of a shape's pair to draw before the server has said anything, read off
    /// the section itself. A placement ghost is a client side entity that never hears from
    /// the server at all, so without this a mirrored section is previewed as its plain twin
    /// and flips over the moment it is put down.
    /// </summary>
    private bool IsDiagonal(EntityUid uid)
    {
        return TryComp<TransitTubeBendComponent>(uid, out var bend) && bend.Diagonal;
    }

    protected override void OnAppearanceChange(EntityUid uid, TransitTubeComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<bool>(uid, TransitTubeVisuals.Diagonal, out var diagonal, args.Component))
            SetShape((uid, component), args.Sprite, diagonal);

        if (!AppearanceSystem.TryGetData<int>(uid, TransitTubeVisuals.Connections, out var connections, args.Component))
            connections = 0;

        foreach (var (direction, layer) in Joints)
        {
            if (!SpriteSystem.LayerMapTryGet((uid, args.Sprite), layer, out var index, false))
                continue;

            SpriteSystem.LayerSetVisible((uid, args.Sprite), index, (connections & (1 << (int) direction)) != 0);
        }
    }

    private void SetShape(Entity<TransitTubeComponent> ent, SpriteComponent sprite, bool diagonal)
    {
        // A stop draws its own doors and has no diagonal twin, so leave its state alone.
        if (ent.Comp.DiagonalShape == null && diagonal)
            return;

        if (!SpriteSystem.LayerMapTryGet((ent.Owner, sprite), TransitTubeJointLayers.Shape, out var index, false))
            return;

        SpriteSystem.LayerSetRsiState((ent.Owner, sprite), index,
            diagonal && ent.Comp.DiagonalShape != null ? ent.Comp.DiagonalShape : ent.Comp.Shape);
    }
}

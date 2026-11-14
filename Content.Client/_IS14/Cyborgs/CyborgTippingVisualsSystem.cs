// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Cyborgs;

/// <summary>
/// A tipped cyborg is drawn on its head, tg's turn(transform, 180).
/// </summary>
public sealed class CyborgTippingVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgTippableComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<CyborgTippableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetRotation((ent.Owner, sprite), ent.Comp.Tipped ? Angle.FromDegrees(180) : Angle.Zero);
    }
}

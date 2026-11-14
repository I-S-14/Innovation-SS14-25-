using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Actions;

/// <summary>Projects retained Goob action icons onto the Wizards sprite layers.</summary>
public sealed partial class LegacyActionIconSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    [SubscribeLocalEvent]
    private void OnStartup(Entity<ActionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Icon is null && ent.Comp.IconOn is null)
            return;
        var sprite = EnsureComp<SpriteComponent>(ent);
        if (ent.Comp.Icon is { } icon)
            SetIcon((ent.Owner, sprite), ActionVisuals.Icon, icon);
        if (ent.Comp.IconOn is { } toggled)
        {
            SetIcon((ent.Owner, sprite), ActionVisuals.IconToggled, toggled);
            _sprite.LayerSetVisible((ent.Owner, sprite), ActionVisuals.IconToggled, ent.Comp.Toggled);
            _sprite.LayerSetVisible((ent.Owner, sprite), ActionVisuals.Icon, !ent.Comp.Toggled);
        }
    }

    private void SetIcon(Entity<SpriteComponent> ent, ActionVisuals key, SpriteSpecifier icon)
    {
        _sprite.LayerMapReserve(ent.AsNullable(), key);
        if (icon is SpriteSpecifier.EntityPrototype)
            _sprite.LayerSetTexture(ent.AsNullable(), key, _sprite.Frame0(icon));
        else
            _sprite.LayerSetSprite(ent.AsNullable(), key, icon);
    }
}

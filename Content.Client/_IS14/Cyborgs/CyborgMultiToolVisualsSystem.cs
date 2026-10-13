// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Cyborgs;

/// <summary>
/// Shows the current mode of a cycling cyborg tool on its sprite.
/// </summary>
public sealed class CyborgMultiToolVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgMultiToolComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CyborgMultiToolComponent, ComponentStartup>(OnStartup);
    }

    private void OnState(Entity<CyborgMultiToolComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent);
    }

    private void OnStartup(Entity<CyborgMultiToolComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent);
    }

    private void UpdateSprite(Entity<CyborgMultiToolComponent> ent)
    {
        if (ent.Comp.Current >= ent.Comp.Modes.Count || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.LayerSetRsiState((ent.Owner, sprite), 0, ent.Comp.Modes[ent.Comp.Current].State);
    }
}

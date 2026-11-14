// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Cyborgs;

/// <summary>
/// Refreshes an open reagent picker when the selected reagent changes.
/// </summary>
public sealed class CyborgReagentUiSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgReagentSynthesizerComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<CyborgReagentSynthesizerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, CyborgReagentUiKey.Key, out var bui))
            bui.Update();
    }
}

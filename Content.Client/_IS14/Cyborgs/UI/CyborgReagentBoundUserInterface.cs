// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._IS14.Cyborgs.UI;

public sealed class CyborgReagentBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private CyborgReagentWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CyborgReagentWindow>();
        _window.ReagentSelected += id => SendPredictedMessage(new CyborgSelectReagentMessage(id));
        Update();
    }

    public override void Update()
    {
        base.Update();

        if (_window == null || !EntMan.TryGetComponent<CyborgReagentSynthesizerComponent>(Owner, out var synth))
            return;

        var reagents = new List<(string, string, Color)>();
        foreach (var id in EntMan.System<CyborgReagentSynthesizerSystem>().GetAvailableReagents((Owner, synth)))
        {
            if (_proto.TryIndex(id, out ReagentPrototype? reagent))
                reagents.Add((id.Id, reagent.LocalizedName, reagent.SubstanceColor));
        }

        _window.Populate(reagents, synth.Selected?.Id);
    }
}

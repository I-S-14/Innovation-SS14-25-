// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.Controls;
using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._IS14.Cyborgs.UI;

/// <summary>
/// Radial menu of what a cyborg dispenser can print, like the RCD's.
/// </summary>
public sealed class CyborgDispenserBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(CreateButtons());
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> CreateButtons()
    {
        if (!EntMan.TryGetComponent<CyborgDispenserComponent>(Owner, out var dispenser))
            yield break;

        for (var i = 0; i < dispenser.Options.Count; i++)
        {
            var option = dispenser.Options[i];
            yield return new RadialMenuActionOption<int>(Select, i)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(option),
                ToolTip = _proto.TryIndex(option, out var proto) ? proto.Name : option.Id,
            };
        }
    }

    private void Select(int index)
    {
        SendPredictedMessage(new CyborgDispenserSelectMessage(index));
        Close();
    }
}

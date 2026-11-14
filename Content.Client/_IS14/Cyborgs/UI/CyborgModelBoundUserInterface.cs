// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Prototypes;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._IS14.Cyborgs.UI;

/// <summary>
/// Window the cyborg uses to pick its model together with a skin.
/// </summary>
public sealed class CyborgModelBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private CyborgModelWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CyborgModelWindow>();
        _window.Confirmed += (model, skin) => SendMessage(new CyborgSelectModelMessage(model, skin));

        var models = _proto.EnumeratePrototypes<CyborgModelPrototype>()
            .Where(model => model.Selectable)
            .OrderBy(model => model.Order)
            .ThenBy(model => Loc.GetString(model.Name))
            .ToList();
        _window.Populate(models);
    }
}

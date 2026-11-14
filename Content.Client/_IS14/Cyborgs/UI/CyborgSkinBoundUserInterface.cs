// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._IS14.Cyborgs.UI;

/// <summary>
/// The model window locked to the cyborg's current model, for a cyborg spawned with one.
/// </summary>
public sealed class CyborgSkinBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private CyborgModelWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CyborgModelWindow>();
        _window.Confirmed += (_, skin) =>
        {
            if (skin != null)
                SendMessage(new CyborgSelectSkinMessage(skin.Value));
        };

        if (EntMan.TryGetComponent<CyborgComponent>(Owner, out var cyborg) &&
            _proto.TryIndex(cyborg.Model, out var model))
            _window.Populate([model], cyborg.Skin);
    }
}

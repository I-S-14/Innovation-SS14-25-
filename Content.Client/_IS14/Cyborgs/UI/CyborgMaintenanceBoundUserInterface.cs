// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Cyborgs.UI;

public sealed class CyborgMaintenanceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CyborgMaintenanceWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CyborgMaintenanceWindow>();
        _window.SetCyborg(Owner);
        _window.EjectBrainPressed += () => SendMessage(new CyborgMaintenanceEjectBrainMessage());
        _window.EjectCellPressed += () => SendMessage(new CyborgMaintenanceEjectCellMessage());
        _window.RemoveUpgradePressed += board => SendMessage(new CyborgMaintenanceRemoveUpgradeMessage(board));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CyborgMaintenanceBuiState cast)
            _window?.UpdateState(cast);
    }
}

using Content.Shared.VendingMachines.Events;

namespace Content.Shared.VendingMachines;

public abstract partial class SharedVendingMachineSystem
{
    [SubscribeLocalEvent]
    private void OnOpenVendingUIAction(Entity<VendingMachineComponent> entity, ref OpenVendingUIActionEvent args)
    {
        if (args.Handled)
            return;
        UISystem.TryToggleUi(entity.Owner, VendingMachineUiKey.Key, args.Performer);
        args.Handled = true;
    }
}

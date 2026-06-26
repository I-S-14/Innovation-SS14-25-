using Content.Shared._IS14.Economy.VendingMachine;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.VendingMachine;

[UsedImplicitly]
public sealed class IS14VendingMachineBoundUserInterface : BoundUserInterface
{
    private IS14VendingMachineWindow? _window;

    public IS14VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<IS14VendingMachineWindow>();
        _window.OnBuyPressed += (tabIndex, itemIndex) =>
            SendMessage(new IS14VendingMachineBuyMessage(tabIndex, itemIndex));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IS14VendingMachineUiState vendState)
            _window?.UpdateState(vendState);
    }
}

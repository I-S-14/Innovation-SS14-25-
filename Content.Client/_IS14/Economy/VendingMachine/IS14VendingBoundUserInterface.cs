using Content.Shared._IS14.Economy.VendingMachine;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.VendingMachine;

/// <summary>
/// IS14 economy vending machine UI.
/// NOTE: the class name deliberately does NOT end with "VendingMachineBoundUserInterface" —
/// the engine resolves BUI types via <c>IReflectionManager.LooseGetType</c>, which matches by
/// FullName suffix, so a longer name would hijack the vanilla lookup and break vanilla machines.
/// </summary>
[UsedImplicitly]
public sealed class IS14VendingBoundUserInterface : BoundUserInterface
{
    private IS14VendingMachineWindow? _window;

    public IS14VendingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
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

using Content.Shared._IS14.Economy.Treasury;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.Treasury;

public sealed class TreasuryConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private TreasuryConsoleWindow? _window;

    public TreasuryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TreasuryConsoleWindow>();
        _window.OnClose += Close;
        _window.OnTransfer += (target, amount) => SendMessage(new TreasuryTransferMessage(target, amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is TreasuryConsoleUiState s)
            _window?.UpdateState(s);
    }
}

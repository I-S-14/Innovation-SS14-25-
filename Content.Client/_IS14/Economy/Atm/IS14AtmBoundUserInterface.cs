using Content.Shared._IS14.Economy.Atm;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.Atm;

public sealed class IS14AtmBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private IS14AtmWindow? _window;

    public IS14AtmBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<IS14AtmWindow>();
        _window.OnClose += Close;

        _window.OnPinSubmitted += (screen, pin) =>
        {
            BoundUserInterfaceMessage msg = screen switch
            {
                IS14AtmScreen.SetPin => new IS14AtmSetPinMessage(pin),
                IS14AtmScreen.EnterPin => new IS14AtmEnterPinMessage(pin),
                _ => new IS14AtmChangePinMessage(pin),
            };
            SendMessage(msg);
        };

        _window.OnWithdraw += amount => SendMessage(new IS14AtmWithdrawMessage(amount));
        _window.OnTransfer += (account, amount) => SendMessage(new IS14AtmTransferMessage(account, amount));
        _window.OnEjectCard += () => SendMessage(new IS14AtmEjectCardMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is IS14AtmUiState s)
            _window?.UpdateState(s);
    }
}

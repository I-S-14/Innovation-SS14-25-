using Content.Shared._IS14.Economy.PaymentTerminal;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.PaymentTerminal;

/// <summary>
/// NOTE: named "…Bui" instead of "…BoundUserInterface" on purpose — the engine resolves BUI
/// types by FullName suffix (LooseGetType), so conventional names risk hijacking upstream lookups.
/// </summary>
[UsedImplicitly]
public sealed class IS14PaymentTerminalBui : BoundUserInterface
{
    [ViewVariables]
    private PaymentTerminalWindow? _window;

    public IS14PaymentTerminalBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaymentTerminalWindow>();
        _window.OnSetCharge += (amount, desc) => SendMessage(new IS14PaymentTerminalSetChargeMessage(amount, desc));
        _window.OnPay += () => SendMessage(new IS14PaymentTerminalPayMessage());
        _window.OnCancel += () => SendMessage(new IS14PaymentTerminalCancelMessage());
        _window.OnBindCard += () => SendMessage(new IS14PaymentTerminalBindCardMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IS14PaymentTerminalUiState s)
            _window?.UpdateState(s);
    }
}

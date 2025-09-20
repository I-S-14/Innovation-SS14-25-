using Content.Shared._IS.Payment;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using System.Text.RegularExpressions;

namespace Content.Client._IS.Payment;

/// <summary>
/// Initializes a <see cref="_IS.Payment.BalanceWithdrawWindow"/> and updates it when new server messages are received.
/// </summary>
[UsedImplicitly]
public sealed class BalanceWithdrawBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BalanceWithdrawWindow? _window;

    public BalanceWithdrawBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BalanceWithdrawWindow>();
        _window.OnWithdrawButtonClicked += OnWithdrawButtonClicked;
    }

    private void OnWithdrawButtonClicked(int value)
    {
        SendPredictedMessage(new WithdrawWindowWithdrawMessage(value));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not WithdrawWindowInterfaceState wState || _window == null)
                return;

            _window.SetBalance(wState.Balance);
            _window.SetAccount(Regex.Replace(wState.PersonalAccount, ".{4}", "$0 ").Trim());
        }
}


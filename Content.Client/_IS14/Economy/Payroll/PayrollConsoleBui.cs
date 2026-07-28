using Content.Shared._IS14.Economy.Payroll;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.Payroll;

public sealed class PayrollConsoleBui : BoundUserInterface
{
    [ViewVariables]
    private PayrollConsoleWindow? _window;

    public PayrollConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PayrollConsoleWindow>();
        _window.OnClose += Close;
        _window.OnSetSalary += (employee, salary) => SendMessage(new PayrollSetSalaryMessage(employee, salary));
        _window.OnBonus += (employee, amount) => SendMessage(new PayrollBonusMessage(employee, amount));
        _window.OnFine += (employee, amount) => SendMessage(new PayrollFineMessage(employee, amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is PayrollConsoleUiState s)
            _window?.UpdateState(s);
    }
}

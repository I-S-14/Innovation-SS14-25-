using Content.Client._IS14.Controls;
using Content.Client._IS14.Economy.Payroll;
using Content.Shared._IS14.Economy.Payroll;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.OS.Apps;

/// <summary>
///     Payroll inside the OS. The layout is <see cref="PayrollConsolePanel"/> — literally the
///     standalone console's own contents, minus its window frame (Docs §12.2).
///
///     It keeps the console's palette rather than the device theme. That is deliberate for the
///     migration: a head who has used the wall console recognises the app immediately, and one
///     layout serving both front ends is what makes the old console safe to keep running.
/// </summary>
public sealed class PayrollAppUi : IS14OsAppUi
{
    private PayrollConsolePanel? _panel;

    private PayrollConsolePanel Panel => _panel ??= new PayrollConsolePanel();

    public override string AppId => "AppPayroll";

    public override Control Root => Panel;

    public override void Setup(IS14OsBui bui)
    {
        base.Setup(bui);

        Panel.OnSetSalary += (employee, salary) =>
            SendAppEvent(AppId, new OsConsoleMessageEvent(new PayrollSetSalaryMessage(employee, salary)));

        Panel.OnBonus += (employee, amount) =>
            SendAppEvent(AppId, new OsConsoleMessageEvent(new PayrollBonusMessage(employee, amount)));

        Panel.OnFine += (employee, amount) =>
            SendAppEvent(AppId, new OsConsoleMessageEvent(new PayrollFineMessage(employee, amount)));
    }

    public override void UpdateState(IS14OsAppState state)
    {
        if (state is OsConsoleState { State: PayrollConsoleUiState payroll })
            Panel.UpdateState(payroll);
    }
}

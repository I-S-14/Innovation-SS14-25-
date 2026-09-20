using Content.Server._IS14.Economy.Payroll;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Payroll as an OS application (Docs §12.2).
///
///     None of the payroll rules live here: the app hands the console's own messages to
///     <see cref="PayrollConsoleSystem"/> and ships back the state that system builds. That is
///     the whole point of the migration — one set of rules, two front ends, and the standalone
///     console stays working the entire time.
///
///     The data component is not carried by the app prototype: which fund a payroll console
///     administers is a property of that console, so the console entity declares
///     <see cref="PayrollConsoleComponent"/> itself and the app is only a window onto it.
/// </summary>
public sealed class IS14OsPayrollAppSystem : EntitySystem
{
    [Dependency] private readonly PayrollConsoleSystem _payroll = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    public const string AppId = "AppPayroll";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PayrollConsoleComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<PayrollConsoleComponent, OsAppEventRaised>(OnAppEvent);
        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);
    }

    private void OnGetState(Entity<PayrollConsoleComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        args.State = new OsConsoleState(_payroll.BuildState(ent));
    }

    private void OnAppEvent(Entity<PayrollConsoleComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsConsoleMessageEvent wrapped)
            return;

        // The OS envelope is what carries who pressed the button; the console's own rules read
        // it off the message, so it has to be stamped back on before handing over.
        wrapped.Message.Actor = args.Actor;
        _payroll.HandleMessage(ent, wrapped.Message);
    }

    /// <summary>
    ///     Any transaction can move the fund balance, so consoles showing one are queued for a
    ///     refresh. The OS throttles these to once a second on its own (§4.5).
    /// </summary>
    private void OnTransaction(EconomyTransactionEvent args)
    {
        var query = EntityQueryEnumerator<PayrollConsoleComponent, IS14OsDeviceComponent>();
        while (query.MoveNext(out var uid, out _, out var device))
        {
            if (device.Open.Contains(AppId))
                _os.MarkDirty(uid);
        }
    }
}

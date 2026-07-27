using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.Payroll;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.Payroll;

/// <summary>
/// Marks a console that lets a department head manage the payroll of everyone paid
/// by <see cref="ManagedAccount"/>: raises, cuts, bonuses and fines.
/// Access to the console itself is gated by the entity's AccessReader; the limits on
/// what a head may do come from the is14.payroll_* cvars.
/// </summary>
[RegisterComponent]
public sealed partial class PayrollConsoleComponent : Component
{
    /// <summary>
    /// Department fund this console administers. Crew are listed here when their salary
    /// is debited from this fund, so the head pays for their own raises.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StationAccountPrototype> ManagedAccount = string.Empty;

    /// <summary>
    /// Whether the head may run payroll operations on themselves.
    /// Off by default — heads are paid from the same fund they administer.
    /// </summary>
    [DataField]
    public bool AllowSelfManagement;

    /// <summary>
    /// Actions taken on this console, oldest first. Trimmed to is14.payroll_log_length.
    /// Local to the console: the station-wide record lives in the economy monitor.
    /// </summary>
    [ViewVariables]
    public readonly List<PayrollLogEntry> Log = new();
}

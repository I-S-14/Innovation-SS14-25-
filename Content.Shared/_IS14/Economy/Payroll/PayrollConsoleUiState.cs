using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Payroll;

[Serializable, NetSerializable]
public sealed class PayrollConsoleUiState : BoundUserInterfaceState
{
    /// <summary>Display name of the department fund this console draws from.</summary>
    public readonly string FundName;

    public readonly int FundBalance;

    /// <summary>Crew paid by this fund, sorted by job title then name.</summary>
    public readonly List<PayrollEmployeeEntry> Employees;

    /// <summary>Credits accumulated in the department's bonus pool.</summary>
    public readonly int BonusPool;

    /// <summary>
    /// Largest single bonus payable right now: the bonus pool, or the fund balance
    /// when the fund can't cover the whole pool.
    /// </summary>
    public readonly int MaxBonus;

    /// <summary>Largest single fine the console allows.</summary>
    public readonly int MaxFine;

    /// <summary>Actions performed on this console, newest first.</summary>
    public readonly List<PayrollLogEntry> Log;

    /// <summary>Localized status line from the last operation (empty — nothing to show).</summary>
    public readonly string Status;

    public PayrollConsoleUiState(
        string fundName,
        int fundBalance,
        List<PayrollEmployeeEntry> employees,
        int bonusPool,
        int maxBonus,
        int maxFine,
        List<PayrollLogEntry> log,
        string status = "")
    {
        FundName = fundName;
        FundBalance = fundBalance;
        Employees = employees;
        BonusPool = bonusPool;
        MaxBonus = maxBonus;
        MaxFine = maxFine;
        Log = log;
        Status = status;
    }
}

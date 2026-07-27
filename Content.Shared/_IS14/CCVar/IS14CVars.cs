using Robust.Shared.Configuration;

namespace Content.Shared._IS14.CCVar;

/// <summary>
/// Innovation Station specific cvars.
/// </summary>
[CVarDefs]
public sealed partial class IS14CVars
{
    /*
     * Payroll — department heads adjusting the pay of their subordinates.
     */

    /// <summary>
    /// Lowest salary a department head may set, as a fraction of the job's base salary.
    /// 0 lets a head cut pay entirely.
    /// </summary>
    public static readonly CVarDef<float> PayrollSalaryMinMultiplier =
        CVarDef.Create("is14.payroll_salary_min_multiplier", 0f, CVar.SERVERONLY);

    /// <summary>
    /// Highest salary a department head may set, as a fraction of the job's base salary.
    /// </summary>
    public static readonly CVarDef<float> PayrollSalaryMaxMultiplier =
        CVarDef.Create("is14.payroll_salary_max_multiplier", 3f, CVar.SERVERONLY);

    /// <summary>
    /// Share of every payment a station account receives that is set aside for bonuses,
    /// and of the account's starting balance. Bonuses are paid out of that accumulated pool,
    /// so over a shift a department can only hand out this fraction of what it earned.
    /// </summary>
    public static readonly CVarDef<float> PayrollBonusIncomeFraction =
        CVarDef.Create("is14.payroll_bonus_income_fraction", 0.05f, CVar.SERVERONLY);

    /// <summary>
    /// Largest single fine a head may collect from a subordinate, in credits.
    /// </summary>
    public static readonly CVarDef<int> PayrollMaxFine =
        CVarDef.Create("is14.payroll_max_fine", 5000, CVar.SERVERONLY);

    /// <summary>
    /// How many actions the payroll console keeps in its local log tab.
    /// </summary>
    public static readonly CVarDef<int> PayrollLogLength =
        CVarDef.Create("is14.payroll_log_length", 100, CVar.SERVERONLY);
}

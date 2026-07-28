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

    /*
     * Gosplan — the plan periods that fund departments based on their performance.
     */

    /// <summary>
    /// Whether the station gets a plan at all. Turning this off leaves departments
    /// with only their starting budgets and whatever they earn themselves.
    /// </summary>
    public static readonly CVarDef<bool> GosplanEnabled =
        CVarDef.Create("is14.gosplan_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Length of one plan period in seconds. Every period each quota is scored and
    /// the funding for the next period is paid out.
    /// </summary>
    public static readonly CVarDef<int> GosplanPeriodSeconds =
        CVarDef.Create("is14.gosplan_period_seconds", 900, CVar.SERVERONLY);

    /// <summary>
    /// Scales every payout the plan hands out. Raise it on servers where departments
    /// starve, lower it where credits pile up.
    /// </summary>
    public static readonly CVarDef<float> GosplanPayoutMultiplier =
        CVarDef.Create("is14.gosplan_payout_multiplier", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Highest fulfillment ratio a quota can be paid for. Overfulfilling past this
    /// stops earning extra credits, so a single runaway department can't print money.
    /// </summary>
    public static readonly CVarDef<float> GosplanOverfulfillmentCap =
        CVarDef.Create("is14.gosplan_overfulfillment_cap", 1.5f, CVar.SERVERONLY);

    /// <summary>
    /// Fulfillment at or above this counts as overfulfillment: the department gets a
    /// bonus pool top-up and competes for the red banner.
    /// </summary>
    public static readonly CVarDef<float> GosplanOverfulfillmentThreshold =
        CVarDef.Create("is14.gosplan_overfulfillment_threshold", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Fulfillment below this counts as a failed quota. Two failures in a row bring
    /// sanctions from Gosplan.
    /// </summary>
    public static readonly CVarDef<float> GosplanFailureThreshold =
        CVarDef.Create("is14.gosplan_failure_threshold", 0.5f, CVar.SERVERONLY);

    /// <summary>
    /// Share of a department's remaining budget confiscated when it fails the plan
    /// twice in a row.
    /// </summary>
    public static readonly CVarDef<float> GosplanSanctionFraction =
        CVarDef.Create("is14.gosplan_sanction_fraction", 0.15f, CVar.SERVERONLY);

    /// <summary>
    /// Extra credits paid straight into the bonus pool of the period's leading
    /// department, on top of its regular funding. This is what heads actually hand
    /// out as bonuses, so winning the plan directly buys goodwill.
    /// </summary>
    public static readonly CVarDef<int> GosplanBannerBonus =
        CVarDef.Create("is14.gosplan_banner_bonus", 3000, CVar.SERVERONLY);

    /// <summary>
    /// How many finished periods the soc-competition board keeps in its history tab.
    /// </summary>
    public static readonly CVarDef<int> GosplanHistoryLength =
        CVarDef.Create("is14.gosplan_history_length", 12, CVar.SERVERONLY);

    /*
     * Fines — Security collecting credits instead of cell time.
     */

    /// <summary>
    /// Largest single fine an officer may write, in credits.
    /// </summary>
    public static readonly CVarDef<int> FineMaxAmount =
        CVarDef.Create("is14.fine_max_amount", 2000, CVar.SERVERONLY);

    /// <summary>
    /// Whether an unpaid fine puts the offender on the wanted list until they pay.
    /// This is what gives a fine teeth: refuse to pay and Security may detain you.
    /// </summary>
    public static readonly CVarDef<bool> FineSetsWantedStatus =
        CVarDef.Create("is14.fine_sets_wanted_status", true, CVar.SERVERONLY);
}

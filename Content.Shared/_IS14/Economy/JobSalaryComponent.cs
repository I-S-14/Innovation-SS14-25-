namespace Content.Shared._IS14.Economy;

/// <summary>
/// Added to a crew member entity at round start.
/// Tracks the account to pay and the salary schedule.
/// </summary>
[RegisterComponent]
public sealed partial class JobSalaryComponent : Component
{
    [DataField]
    public int AccountNumber;

    [DataField]
    public int Salary;

    /// <summary>Seconds between payments, copied from JobEconomyPrototype.</summary>
    [DataField]
    public int SalaryIntervalSeconds;

    /// <summary>Absolute game time of the next salary payment.</summary>
    [DataField]
    public TimeSpan NextPaymentTime;

    /// <summary>ID card entity that announces salary payments in IC chat.</summary>
    [DataField]
    public EntityUid? IdCardEntity;

    /// <summary>Station whose fund pays this salary.</summary>
    [DataField]
    public EntityUid? Station;

    /// <summary>
    /// Station account prototype ID the salary is debited from.
    /// Null — salary is paid without a debit (legacy behavior).
    /// </summary>
    [DataField]
    public string? PayerAccount;

    /// <summary>Accumulated unpaid salary from ticks when the fund was empty.</summary>
    [DataField]
    public int OwedSalary;
}

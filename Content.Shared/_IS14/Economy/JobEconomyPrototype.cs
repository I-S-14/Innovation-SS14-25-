using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Economy;

/// <summary>
/// Defines starting balance and salary for a job.
/// The prototype ID must match the corresponding JobPrototype ID.
/// </summary>
[Prototype("jobEconomy")]
public sealed partial class JobEconomyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>Minimum starting balance (inclusive).</summary>
    [DataField]
    public int MinBalance;

    /// <summary>Maximum starting balance (inclusive).</summary>
    [DataField]
    public int MaxBalance;

    /// <summary>Credits added to the account each salary interval.</summary>
    [DataField]
    public int Salary;

    /// <summary>Seconds between salary payments. Default: 10 minutes.</summary>
    [DataField]
    public int SalaryIntervalSeconds = 600;
}

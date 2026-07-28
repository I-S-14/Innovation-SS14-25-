using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Economy;

/// <summary>
/// Defines a named bank account to be created at round start for the station.
/// </summary>
[Prototype("stationBankAccount")]
public sealed partial class StationAccountPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Human-readable name shown in banking UIs.</summary>
    [DataField]
    public string DisplayName = string.Empty;

    /// <summary>Credits placed in the account at round start.</summary>
    [DataField]
    public int InitialBalance;
}

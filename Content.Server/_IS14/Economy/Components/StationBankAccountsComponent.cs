namespace Content.Server._IS14.Economy.Components;

/// <summary>
/// Automatically added to a station entity by <see cref="StationBankAccountSystem"/>.
/// Maps stationBankAccount prototype IDs to live bank account numbers.
/// </summary>
[RegisterComponent]
public sealed partial class StationBankAccountsComponent : Component
{
    /// <summary>
    /// Key: stationBankAccount prototype ID (e.g. "StationTreasury").
    /// Value: account number in <see cref="BankManagerSystem"/>.
    /// </summary>
    public Dictionary<string, int> AccountNumbers = new();
}

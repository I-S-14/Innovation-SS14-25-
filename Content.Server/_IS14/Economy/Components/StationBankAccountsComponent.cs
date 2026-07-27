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

    /// <summary>
    /// Credits set aside for bonuses, per station account prototype ID.
    /// Seeded from the account's starting balance and grown by a share of every payment
    /// the account receives, so only a slice of what a department earns can be handed out
    /// as bonuses no matter how large the budget gets.
    /// </summary>
    public Dictionary<string, int> BonusPools = new();
}

namespace Content.Shared._IS14.Economy.EconomyMonitor;

/// <summary>
/// Raised (broadcast) whenever a bank account balance changes.
/// </summary>
public sealed class EconomyTransactionEvent : EntityEventArgs
{
    public readonly int AccountNumber;
    public readonly int Delta;
    public readonly int NewBalance;
    public readonly string Description;
    /// <summary>Entity responsible for this transaction (e.g. the vending machine entity).</summary>
    public readonly EntityUid? SourceEntity;

    public EconomyTransactionEvent(
        int accountNumber,
        int delta,
        int newBalance,
        string description,
        EntityUid? sourceEntity = null)
    {
        AccountNumber = accountNumber;
        Delta = delta;
        NewBalance = newBalance;
        Description = description;
        SourceEntity = sourceEntity;
    }
}

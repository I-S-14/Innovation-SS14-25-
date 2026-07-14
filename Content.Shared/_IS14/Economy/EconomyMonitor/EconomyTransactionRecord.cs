using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.EconomyMonitor;

[Serializable, NetSerializable]
public sealed class EconomyTransactionRecord
{
    public TimeSpan Timestamp;
    public int AccountNumber;
    public int Delta;
    public int NewBalance;
    public string Description;
    /// <summary>NetEntity of the entity that caused this transaction (e.g. a vending machine), if any.</summary>
    public NetEntity? SourceEntity;
    /// <summary>World coordinates where the transaction occurred.</summary>
    public NetCoordinates? Location;

    public EconomyTransactionRecord(
        TimeSpan timestamp,
        int accountNumber,
        int delta,
        int newBalance,
        string description,
        NetEntity? sourceEntity = null,
        NetCoordinates? location = null)
    {
        Timestamp = timestamp;
        AccountNumber = accountNumber;
        Delta = delta;
        NewBalance = newBalance;
        Description = description;
        SourceEntity = sourceEntity;
        Location = location;
    }
}

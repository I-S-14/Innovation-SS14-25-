using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.EconomyMonitor;

[Serializable, NetSerializable]
public sealed class EconomyTransactionRecord
{
    /// <summary>Server-assigned unique id, used for deleting and printing selected records.</summary>
    public int Id;
    public TimeSpan Timestamp;
    public int AccountNumber;
    public int Delta;
    public int NewBalance;
    public string Description;
    /// <summary>NetEntity of the entity that caused this transaction (e.g. a vending machine), if any.</summary>
    public NetEntity? SourceEntity;
    /// <summary>World coordinates where the transaction occurred.</summary>
    public NetCoordinates? Location;
    /// <summary>Records sharing a group belong to one purchase (payment + revenue + tax) and collapse into a single log row.</summary>
    public Guid? GroupId;

    public EconomyTransactionRecord(
        int id,
        TimeSpan timestamp,
        int accountNumber,
        int delta,
        int newBalance,
        string description,
        NetEntity? sourceEntity = null,
        NetCoordinates? location = null,
        Guid? groupId = null)
    {
        Id = id;
        Timestamp = timestamp;
        AccountNumber = accountNumber;
        Delta = delta;
        NewBalance = newBalance;
        Description = description;
        SourceEntity = sourceEntity;
        Location = location;
        GroupId = groupId;
    }
}

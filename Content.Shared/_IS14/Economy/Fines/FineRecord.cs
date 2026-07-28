using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Fines;

/// <summary>
/// A fine written by Security. Lives on the station until it is paid or voided —
/// paying is voluntary, which is exactly what makes refusing to pay a decision.
/// </summary>
[Serializable, NetSerializable]
public sealed class FineRecord
{
    public uint Id;

    /// <summary>Name of the offender as written on the fine.</summary>
    public string TargetName = string.Empty;

    /// <summary>
    /// Bank account the fine was matched to when it was written, if the offender's
    /// ID card could be found. Fines can still be paid from any card in that name.
    /// </summary>
    public int? AccountNumber;

    /// <summary>Station record the offender was picked from, used to manage their wanted status.</summary>
    public uint? RecordId;

    public string OfficerName = string.Empty;

    /// <summary>Localized name of the article charged.</summary>
    public string Article = string.Empty;

    public int Amount;

    public bool Paid;

    /// <summary>Cancelled by an officer. Kept in the list so mistakes stay visible.</summary>
    public bool Voided;

    /// <summary>Round time the fine was written at.</summary>
    public TimeSpan Time;

    public FineRecord()
    {
    }

    public FineRecord(
        uint id,
        string targetName,
        int? accountNumber,
        uint? recordId,
        string officerName,
        string article,
        int amount,
        TimeSpan time)
    {
        Id = id;
        TargetName = targetName;
        AccountNumber = accountNumber;
        RecordId = recordId;
        OfficerName = officerName;
        Article = article;
        Amount = amount;
        Time = time;
    }

    /// <summary>A fine that still counts against the offender.</summary>
    public bool Outstanding => !Paid && !Voided;
}

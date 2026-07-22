using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Treasury;

/// <summary>One station account row shown in the treasury console.</summary>
[Serializable, NetSerializable]
public sealed class TreasuryAccountEntry
{
    /// <summary>stationBankAccount prototype ID.</summary>
    public readonly string ProtoId;

    public readonly string DisplayName;

    public readonly int Balance;

    /// <summary>True for the treasury account itself (transfer source).</summary>
    public readonly bool IsTreasury;

    public TreasuryAccountEntry(string protoId, string displayName, int balance, bool isTreasury)
    {
        ProtoId = protoId;
        DisplayName = displayName;
        Balance = balance;
        IsTreasury = isTreasury;
    }
}

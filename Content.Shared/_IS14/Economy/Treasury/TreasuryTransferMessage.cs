using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Treasury;

/// <summary>Client request to transfer credits from the treasury to a department fund.</summary>
[Serializable, NetSerializable]
public sealed class TreasuryTransferMessage : BoundUserInterfaceMessage
{
    /// <summary>stationBankAccount prototype ID of the receiving fund.</summary>
    public readonly string TargetAccount;

    public readonly int Amount;

    public TreasuryTransferMessage(string targetAccount, int amount)
    {
        TargetAccount = targetAccount;
        Amount = amount;
    }
}

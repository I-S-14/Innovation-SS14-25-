using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Fines;

[Serializable, NetSerializable]
public enum FineCartridgeAction : byte
{
    /// <summary>Write a new fine against the selected crew member.</summary>
    Issue,

    /// <summary>Cancel a fine that shouldn't have been written.</summary>
    Void,
}

[Serializable, NetSerializable]
public sealed class FineCartridgeUiMessageEvent : CartridgeMessageEvent
{
    public readonly FineCartridgeAction Action;

    /// <summary>Station record of the offender. Used by <see cref="FineCartridgeAction.Issue"/>.</summary>
    public readonly uint RecordId;

    /// <summary>Article being charged.</summary>
    public readonly string PresetId;

    /// <summary>Penalty in credits, which the officer may have adjusted.</summary>
    public readonly int Amount;

    /// <summary>Fine being cancelled. Used by <see cref="FineCartridgeAction.Void"/>.</summary>
    public readonly uint FineId;

    public FineCartridgeUiMessageEvent(FineCartridgeAction action, uint recordId, string presetId, int amount, uint fineId)
    {
        Action = action;
        RecordId = recordId;
        PresetId = presetId;
        Amount = amount;
        FineId = fineId;
    }
}

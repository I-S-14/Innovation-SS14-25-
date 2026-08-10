// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.BloodDonation;

[Serializable, NetSerializable]
public enum BloodDonationConsoleUiKey : byte
{
    Key,
}

/// <summary>What the console has to say for itself before any numbers are worth reading.</summary>
[Serializable, NetSerializable]
public enum BloodDonationState : byte
{
    /// <summary>No bed linked. Somebody has to go and connect one.</summary>
    Unlinked,

    /// <summary>Bed linked, nobody on it.</summary>
    Empty,

    /// <summary>Donor on the bed, but no needle in them yet.</summary>
    Waiting,

    /// <summary>Needle in and blood moving.</summary>
    Drawing,

    /// <summary>Needle in, nothing moving — bag full, drip set to inject, or run dry.</summary>
    Stalled,
}

/// <summary>
/// Everything the doctor watches a donation by.
/// </summary>
/// <remarks>
/// Volumes are floats rather than <c>FixedPoint2</c> because none of this is arithmetic —
/// it is a readout, and the numbers that decide anything are recomputed on the server when
/// a button is actually pressed.
/// </remarks>
[Serializable, NetSerializable]
public sealed class BloodDonationConsoleUiState : BoundUserInterfaceState
{
    public readonly BloodDonationState State;

    /// <summary>Donor's name, or empty when there is nobody on the bed.</summary>
    public readonly string Donor;

    /// <summary>Donor's blood level as a fraction of normal.</summary>
    public readonly float BloodLevel;

    /// <summary>Whether that level has fallen far enough to be worth saying out loud.</summary>
    public readonly bool BloodLow;

    /// <summary>Units taken from this donor since they lay down.</summary>
    public readonly float Drawn;

    /// <summary>What is in the bag on the drip, and how much it holds.</summary>
    public readonly float PackVolume;
    public readonly float PackCapacity;

    /// <summary>Whether the donor is clean enough for the station to buy from.</summary>
    public readonly bool Clean;

    /// <summary>Units the station will still pay this person for, this shift.</summary>
    public readonly float QuotaLeft;

    /// <summary>Credits owed for the sitting so far.</summary>
    public readonly int Payout;

    /// <summary>Whether the payout button will do anything, and why it might not.</summary>
    public readonly bool CanPay;
    public readonly string PayBlockedReason;

    /// <summary>Whether there is a needle to pull.</summary>
    public readonly bool CanStop;

    /// <summary>Credits per unit the console is currently set to pay, and the allowed range.</summary>
    public readonly int Rate;
    public readonly int MinRate;
    public readonly int MaxRate;

    /// <summary>Whether the console will pull the needle by itself at the safety line.</summary>
    public readonly bool AutoStop;

    /// <summary>Where that line is, as a fraction of full blood, so the screen can name it.</summary>
    public readonly float WarnLevel;

    public BloodDonationConsoleUiState(
        BloodDonationState state,
        string donor,
        float bloodLevel,
        bool bloodLow,
        float drawn,
        float packVolume,
        float packCapacity,
        bool clean,
        float quotaLeft,
        int payout,
        bool canPay,
        string payBlockedReason,
        bool canStop,
        int rate,
        int minRate,
        int maxRate,
        bool autoStop,
        float warnLevel)
    {
        Rate = rate;
        MinRate = minRate;
        MaxRate = maxRate;
        AutoStop = autoStop;
        WarnLevel = warnLevel;
        State = state;
        Donor = donor;
        BloodLevel = bloodLevel;
        BloodLow = bloodLow;
        Drawn = drawn;
        PackVolume = packVolume;
        PackCapacity = packCapacity;
        Clean = clean;
        QuotaLeft = quotaLeft;
        Payout = payout;
        CanPay = canPay;
        PayBlockedReason = payBlockedReason;
        CanStop = canStop;
    }
}

/// <summary>Pull the needle. The doctor decides when a donation is over, not the machine.</summary>
[Serializable, NetSerializable]
public sealed class BloodDonationStopMessage : BoundUserInterfaceMessage;

/// <summary>Print the donor's money.</summary>
[Serializable, NetSerializable]
public sealed class BloodDonationPayoutMessage : BoundUserInterfaceMessage;

/// <summary>Set what the station pays per unit. Clamped to the console's range on arrival.</summary>
[Serializable, NetSerializable]
public sealed class BloodDonationSetRateMessage(int rate) : BoundUserInterfaceMessage
{
    public readonly int Rate = rate;
}

/// <summary>Turn the safety cutoff on or off.</summary>
[Serializable, NetSerializable]
public sealed class BloodDonationAutoStopMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.BloodType;

[Serializable, NetSerializable]
public enum BloodTestKitUiKey : byte
{
    Key,
}

/// <summary>
/// Separate from the analyser's key so the strip can have its own screen. The two devices
/// share a state and a reaction and share nothing else — one is an instrument face, the
/// other is a piece of card, and that difference is most of what the strip is for.
/// </summary>
[Serializable, NetSerializable]
public enum BloodTestStripUiKey : byte
{
    Key,
}

/// <summary>How much the analyser managed to say.</summary>
[Serializable, NetSerializable]
public enum BloodTestOutcome : byte
{
    /// <summary>A group was read.</summary>
    Typed,

    /// <summary>There is blood, but nothing on file matches it. Animals, xenos, novelties.</summary>
    Untyped,

    /// <summary>Nothing to read.</summary>
    NoBlood,
}

/// <summary>
/// One well on the card: an antibody, and whether the sample clumped in it.
/// </summary>
/// <remarks>
/// The wells are generated from whatever antigens exist for the sample's blood, so an
/// antigen added to the game later shows up on the card without the card being told.
/// </remarks>
[Serializable, NetSerializable]
public readonly record struct BloodTestWellState(
    string ShortName,
    string Name,
    bool Positive);

[Serializable, NetSerializable]
public sealed class BloodTestKitUiState : BoundUserInterfaceState
{
    /// <summary>Who or what was tested, already made presentable.</summary>
    public readonly string Sample;

    public readonly BloodTestOutcome Outcome;

    /// <summary>Wells to run, left to right. Empty on <see cref="BloodTestOutcome.NoBlood"/>.</summary>
    public readonly List<BloodTestWellState> Wells;

    /// <summary>LocId of the group's short form, e.g. "AB+". Null unless typed.</summary>
    public readonly string? ShortName;

    /// <summary>LocId of the group spelled out. Null unless typed.</summary>
    public readonly string? Name;

    public readonly Color Color;

    /// <summary>
    /// Whether to run the wells rather than showing them already settled. Off when the card
    /// has been sitting developed in somebody's pocket, so re-reading it is instant.
    /// </summary>
    public readonly bool Animate;

    public BloodTestKitUiState(
        string sample,
        BloodTestOutcome outcome,
        List<BloodTestWellState> wells,
        string? shortName,
        string? name,
        Color color,
        bool animate = true)
    {
        Sample = sample;
        Outcome = outcome;
        Wells = wells;
        ShortName = shortName;
        Name = name;
        Color = color;
        Animate = animate;
    }
}

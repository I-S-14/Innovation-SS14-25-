// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Cord;

/// <summary>
/// Blinks the whole cord at once. Cheaper to read at a glance than a travelling band,
/// so it suits a warning — an overloaded line, a hose about to burst — where the point
/// is that something is wrong rather than that something is moving.
/// </summary>
public sealed partial class FlashCordEffect : CordEffect
{
    [DataField]
    public Color Color = Color.FromHex("#E05050");

    /// <summary>Blinks per second.</summary>
    [DataField]
    public float Frequency = 2f;

    /// <summary>Fraction of each blink the cord spends lit, 0..1.</summary>
    [DataField]
    public float Duty = 0.5f;

    /// <summary>Whether the cord has to be flagged as energized for this to show.</summary>
    [DataField]
    public bool RequireEnergized = true;

    public override Color GetColor(in CordEffectArgs args)
    {
        if (RequireEnergized && !args.Energized)
            return args.Base;

        var phase = args.Time * Frequency % 1f;

        return phase < Duty ? Color : args.Base;
    }
}

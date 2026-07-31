// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Cord;

/// <summary>
/// Runs a bright band along the cord — current down a cable, fluid down a hose. The
/// band has a hard leading edge and a short tail rather than pulsing the whole cord at
/// once, which is what makes it read as travelling instead of blinking.
/// </summary>
public sealed partial class PulseCordEffect : CordEffect
{
    /// <summary>Colour at the centre of the band.</summary>
    [DataField]
    public Color Color = Color.FromHex("#7FD8FF");

    /// <summary>Segments per second the band travels at. Negative runs it backwards.</summary>
    [DataField]
    public float Speed = 7f;

    /// <summary>Half-length of the band, in segments.</summary>
    [DataField]
    public float Width = 2.5f;

    /// <summary>Segments between one band and the next.</summary>
    [DataField]
    public float Spacing = 5f;

    /// <summary>
    /// Whether the cord has to be flagged as energized for this to show. Left on for
    /// anything that is only sometimes live, off for a cord that always glows.
    /// </summary>
    [DataField]
    public bool RequireEnergized = true;

    public override Color GetColor(in CordEffectArgs args)
    {
        if (RequireEnergized && !args.Energized)
            return args.Base;

        // Remainder rather than modulo, so the distance is signed and the band is
        // symmetrical about its centre instead of jumping at the wrap point.
        var distance = MathF.IEEERemainder(args.Index - args.Time * Speed, MathF.Max(0.01f, Spacing));
        var glow = Math.Clamp(1f - MathF.Abs(distance) / MathF.Max(0.01f, Width), 0f, 1f);

        return Color.InterpolateBetween(args.Base, Color, glow * glow);
    }
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._IS14.Medical.BloodType;

/// <summary>
/// How a typing card is drawn: backlit on a device, or printed on a piece of paper.
/// </summary>
/// <remarks>
/// The two readers show the same three wells and have to feel nothing alike. An analyser is
/// a screen in a dark room; a strip is a bit of card somebody bled on. Keeping that as a
/// palette rather than two copies of the control means the reaction itself — the soaking,
/// the clumping, the timing — stays one piece of code with one behaviour.
///
/// Note what is not in here: a colour per antigen. Blood is red in all three wells, and the
/// only thing that differs between them is whether it clots.
/// </remarks>
public readonly record struct BloodTestPalette(
    Color Plate,
    Color Rim,
    Color RimLit,
    Color Blood,
    Color Clump,
    Color Title,
    Color Pending,
    Color Negative)
{
    /// <summary>The analyser: lit wells on a dark instrument face.</summary>
    public static readonly BloodTestPalette Screen = new(
        Plate: Color.FromHex("#0E1119"),
        Rim: Color.FromHex("#39445C"),
        RimLit: Color.FromHex("#8A9AC0"),
        Blood: Color.FromHex("#8E2F30"),
        Clump: Color.FromHex("#D2544B"),
        Title: Color.FromHex("#7C8AA8"),
        Pending: Color.FromHex("#5A6478"),
        Negative: Color.FromHex("#6E7A90"));

    /// <summary>The strip: reagent pads printed on card, read by eye.</summary>
    public static readonly BloodTestPalette Paper = new(
        Plate: Color.FromHex("#EFE8D2"),
        Rim: Color.FromHex("#8C7F63"),
        RimLit: Color.FromHex("#4E4535"),
        Blood: Color.FromHex("#A83A32"),
        Clump: Color.FromHex("#5E1712"),
        Title: Color.FromHex("#5A4F3C"),
        Pending: Color.FromHex("#A79B82"),
        Negative: Color.FromHex("#8A7F68"));
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Utility;

namespace Content.Client._IS14.Economy.Gosplan;

/// <summary>
/// One place for the board's colours, so the plan tab, the history tab and the
/// banner block all agree on what "failing" or "Engineering" looks like.
/// </summary>
public static class GosplanPalette
{
    public static readonly Color Gold = Color.FromHex("#E0C060");
    public static readonly Color GoldDim = Color.FromHex("#BF8A40");
    public static readonly Color Text = Color.FromHex("#B8C4D8");
    public static readonly Color TextDim = Color.FromHex("#8898A8");

    public static readonly Color Over = Color.FromHex("#4FD14F");
    public static readonly Color Good = Color.FromHex("#3ABB3A");
    public static readonly Color OnTrack = Color.FromHex("#C9A53B");
    public static readonly Color Failing = Color.FromHex("#BF4040");

    public static readonly Color Track = Color.FromHex("#0E1220");
    public static readonly Color FailZone = Color.FromHex("#2A1519");
    public static readonly Color Segment = Color.FromHex("#0A0D18");
    public static readonly Color Border = Color.FromHex("#3A4460");

    private static readonly Color FundDefault = Color.FromHex("#6C7A94");

    private static readonly Dictionary<string, Color> FundColors = new()
    {
        ["StationCommand"] = Color.FromHex("#5C7FD0"),
        ["StationEngineering"] = Color.FromHex("#E08B2E"),
        ["StationMedical"] = Color.FromHex("#4FA8C8"),
        ["StationSecurity"] = Color.FromHex("#C0503E"),
        ["StationResearch"] = Color.FromHex("#9B6FD0"),
        ["StationCargo"] = Color.FromHex("#C2A24A"),
        ["StationService"] = Color.FromHex("#4FB07A"),
    };

    public static Color Fund(string fundId) =>
        FundColors.TryGetValue(fundId, out var color) ? color : FundDefault;

    /// <summary>
    /// Wraps text in a colour tag for a <c>RichTextLabel</c>. Department names come from
    /// prototypes, but they still go through the escaper — a stray bracket must not eat
    /// the rest of the line.
    /// </summary>
    public static string Markup(Color color, string text) =>
        $"[color={color.ToHexNoAlpha()}]{FormattedMessage.EscapeText(text)}[/color]";

    /// <summary>
    /// Colour of a fulfillment figure. The thresholds come from the server, so a
    /// server that moves them still gets a board that agrees with its own payouts.
    /// </summary>
    public static Color ForFulfillment(float fulfillment, float failThreshold, float overThreshold)
    {
        if (fulfillment >= overThreshold)
            return Over;

        if (fulfillment < failThreshold)
            return Failing;

        // Between the two lines: the closer to the target, the greener.
        var span = MathF.Max(0.01f, overThreshold - failThreshold);
        var t = Math.Clamp((fulfillment - failThreshold) / span, 0f, 1f);
        return Color.InterpolateBetween(OnTrack, Good, t);
    }
}

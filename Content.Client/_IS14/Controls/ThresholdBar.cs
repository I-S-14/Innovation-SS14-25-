// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A continuous fill that changes colour as it crosses two marked lines.
///
///     The lines are the point of it. A plain bar says how much is left; this says how
///     much is left <i>before something stops working</i>, which is the question anyone
///     watching a condition readout is actually asking. Ticks are drawn at both thresholds
///     so the numbers do not have to be remembered.
///
///     Domain-neutral on purpose: plating condition, hull integrity, reactor margin and
///     tank charge are all the same shape of readout.
/// </summary>
public sealed class ThresholdBar : Control
{
    /// <summary>How full, 0 to 1.</summary>
    public float Fraction { get; set; }

    /// <summary>
    ///     Below this the bar is <see cref="LowColor"/>. The lower of the two lines.
    /// </summary>
    public float LowThreshold { get; set; } = 0.33f;

    /// <summary>
    ///     Below this — but above <see cref="LowThreshold"/> — the bar is
    ///     <see cref="WarnColor"/>.
    /// </summary>
    public float WarnThreshold { get; set; } = 0.66f;

    public Color LowColor { get; set; } = IS14Palette.Bad;
    public Color WarnColor { get; set; } = IS14Palette.Warn;
    public Color FullColor { get; set; } = IS14Palette.Good;
    public Color BackgroundColor { get; set; } = IS14Palette.Panel;
    public Color TickColor { get; set; } = IS14Palette.Backdrop;

    /// <summary>
    ///     Whether the threshold lines are drawn. Off for a bar whose thresholds are
    ///     merely colour changes rather than something the reader must aim at.
    /// </summary>
    public bool ShowTicks { get; set; } = true;

    public ThresholdBar()
    {
        MinSize = new Vector2(0, 5);
        MouseFilter = MouseFilterMode.Ignore;
    }

    public Color FillColor => Fraction <= LowThreshold
        ? LowColor
        : Fraction <= WarnThreshold
            ? WarnColor
            : FullColor;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        handle.DrawRect(box, BackgroundColor);

        var fill = Math.Clamp(Fraction, 0f, 1f);

        if (fill > 0f)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + box.Width * fill, box.Bottom), FillColor);

        if (!ShowTicks)
            return;

        var width = MathF.Max(1f, MathF.Round(UIScale));

        foreach (var mark in new[] { WarnThreshold, LowThreshold })
        {
            var tick = box.Left + box.Width * Math.Clamp(mark, 0f, 1f);
            handle.DrawRect(new UIBox2(tick, box.Top, tick + width, box.Bottom), TickColor);
        }
    }
}

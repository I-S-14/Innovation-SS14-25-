// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Controls;

/// <summary>
///     An integer allowance drawn as discrete cells rather than a smooth bar.
///
///     Use it wherever the number is countable and small — module complexity, cargo slots,
///     charges, crew berths. Counting notches answers "is there room for one more?" at a
///     glance, which a percentage never does.
///
///     Spent cells are solid, free ones are empty outlines. A row of filled grey cells
///     reads as "used up somehow"; an empty socket reads as room, which is the question
///     being asked.
/// </summary>
public sealed class SegmentBar : Control
{
    /// <summary>Cells already spent.</summary>
    public int Used { get; set; }

    /// <summary>Cells available in total.</summary>
    public int Max { get; set; }

    /// <summary>
    ///     How many cells from the end count as "nearly full" and get the warning colour.
    ///     Zero disables the warning band.
    /// </summary>
    public int WarnSlack { get; set; } = 2;

    public Color FillColor { get; set; } = IS14Palette.Accent;
    public Color WarnColor { get; set; } = IS14Palette.Warn;
    public Color OverColor { get; set; } = IS14Palette.Bad;
    public Color EmptyColor { get; set; } = IS14Palette.Border;

    public SegmentBar()
    {
        MinSize = new Vector2(0, 12);
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Max <= 0)
            return;

        var box = PixelSizeBox;
        var gap = MathF.Max(1f, MathF.Round(UIScale));
        var cell = (box.Width - gap * (Max - 1)) / Max;

        if (cell <= 0f)
            return;

        var over = Used > Max;
        var edge = MathF.Max(1f, MathF.Round(UIScale));

        for (var i = 0; i < Max; i++)
        {
            var left = box.Left + i * (cell + gap);
            var rect = new UIBox2(left, box.Top, left + cell, box.Bottom);

            if (i >= Used)
            {
                handle.DrawRect(rect, EmptyColor, filled: false);
                continue;
            }

            var color = over
                ? OverColor
                : WarnSlack > 0 && i >= Max - WarnSlack
                    ? WarnColor
                    : FillColor;

            handle.DrawRect(rect, color);
        }

        // Over budget: the overflow has nowhere to go on the bar, so it is drawn as a
        // hard edge down the right-hand side rather than being silently dropped.
        if (over)
            handle.DrawRect(new UIBox2(box.Right - edge, box.Top, box.Right, box.Bottom), OverColor);
    }
}

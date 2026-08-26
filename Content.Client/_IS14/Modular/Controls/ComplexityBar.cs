// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Modular.Controls;

/// <summary>
///     The complexity budget as discrete slots rather than a smooth bar: it is an integer
///     allowance, and counting the notches tells the player how much room is left for one
///     more module at a glance.
/// </summary>
public sealed class ComplexityBar : Control
{
    /// <summary>Complexity spent by installed modules.</summary>
    public int Used { get; set; }

    /// <summary>Total the chassis can carry.</summary>
    public int Max { get; set; }

    public ComplexityBar()
    {
        MinSize = new Vector2(0, 12);
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

            // Spent slots are solid, free ones are empty outlines. A row of filled grey
            // cells reads as "used up in a way I do not understand"; an empty socket
            // reads as room for one more module, which is the question being asked.
            if (i >= Used)
            {
                handle.DrawRect(rect, ChassisStyle.Border, filled: false);
                continue;
            }

            var color = over
                ? ChassisStyle.Bad
                : i >= Max - 2
                    ? ChassisStyle.Warn
                    : ChassisStyle.Accent;

            handle.DrawRect(rect, color);
        }

        // Over budget: the overflow has nowhere to go on the bar, so it is drawn as a
        // hard edge down the right-hand side rather than being silently dropped.
        if (over)
            handle.DrawRect(new UIBox2(box.Right - edge, box.Top, box.Right, box.Bottom), ChassisStyle.Bad);
    }
}

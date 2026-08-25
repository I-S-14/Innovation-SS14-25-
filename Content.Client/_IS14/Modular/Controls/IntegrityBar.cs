// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Modular.Controls;

/// <summary>
///     Plating condition. A plain fill rather than notches — condition is continuous,
///     and the break threshold is marked with a tick so the player can see how close a
///     piece is to dropping its hardpoints instead of having to remember the number.
/// </summary>
public sealed class IntegrityBar : Control
{
    /// <summary>Condition remaining, 0 to 1.</summary>
    public float Fraction { get; set; }

    /// <summary>Fraction at which the piece stops carrying modules.</summary>
    public float Threshold { get; set; } = 0.5f;

    public IntegrityBar()
    {
        MinSize = new Vector2(0, 5);
        MouseFilter = MouseFilterMode.Ignore;
    }

    /// <summary>
    ///     Green while healthy, amber approaching the threshold, red once past it —
    ///     the same three-step reading the rest of the readout uses.
    /// </summary>
    public Color FillColor => Fraction <= Threshold
        ? ChassisStyle.Bad
        : Fraction <= Threshold + 0.25f
            ? ChassisStyle.Warn
            : ChassisStyle.Good;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        handle.DrawRect(box, ChassisStyle.Panel);

        var fill = Math.Clamp(Fraction, 0f, 1f);

        if (fill > 0f)
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + box.Width * fill, box.Bottom), FillColor);

        // The line the piece must not fall below.
        var tick = box.Left + box.Width * Math.Clamp(Threshold, 0f, 1f);
        var width = MathF.Max(1f, MathF.Round(UIScale));

        handle.DrawRect(new UIBox2(tick, box.Top, tick + width, box.Bottom), ChassisStyle.Backdrop);
    }
}

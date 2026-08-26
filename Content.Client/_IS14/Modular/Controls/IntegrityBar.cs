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
    public float ModuleThreshold { get; set; } = 0.66f;

    /// <summary>Fraction at which the piece stops holding pressure.</summary>
    public float UnsealThreshold { get; set; } = 0.33f;

    public IntegrityBar()
    {
        MinSize = new Vector2(0, 5);
        MouseFilter = MouseFilterMode.Ignore;
    }

    /// <summary>
    ///     Green while the piece is whole, amber once its modules are gone, red once it
    ///     will not close — the two colours match the two lines drawn on the bar.
    /// </summary>
    public Color FillColor => Fraction <= UnsealThreshold
        ? ChassisStyle.Bad
        : Fraction <= ModuleThreshold
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

        // The two lines: modules first, pressure second.
        var width = MathF.Max(1f, MathF.Round(UIScale));

        foreach (var mark in new[] { ModuleThreshold, UnsealThreshold })
        {
            var tick = box.Left + box.Width * Math.Clamp(mark, 0f, 1f);
            handle.DrawRect(new UIBox2(tick, box.Top, tick + width, box.Bottom), ChassisStyle.Backdrop);
        }
    }
}

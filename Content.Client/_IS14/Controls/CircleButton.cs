// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A big round button: a ring with a disc inside it, the way every phone camera has drawn
///     its shutter for fifteen years.
///
///     Round is not decoration here. A control that is the single most important thing on a
///     screen should not look like the four small square buttons next to it, and a circle is
///     read as "press this" long before the caption is.
/// </summary>
public sealed class CircleButton : ContainerButton
{
    private IS14ThemePalette _palette = IS14ThemePalette.Default;

    /// <summary>Thickness of the outer ring, in virtual pixels.</summary>
    public float RingWidth { get; set; } = 3f;

    /// <summary>Gap between the ring and the disc.</summary>
    public float RingGap { get; set; } = 3f;

    public float Diameter
    {
        get => MinSize.X;
        set => MinSize = new Vector2(value, value);
    }

    public IS14ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            DrawModeChanged();
        }
    }

    public CircleButton()
    {
        Diameter = 48f;
        DrawModeChanged();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // The base constructor gets here before our fields are assigned.
        if (_palette == null!)
            return;

        // The circles are drawn by hand; the button's own box must not show through behind them.
        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        Modulate = DrawMode == DrawModeEnum.Disabled ? new Color(1f, 1f, 1f, 0.55f) : Color.White;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = (Vector2) PixelSize;
        var center = size / 2f;
        var outer = MathF.Min(size.X, size.Y) / 2f - UIScale;

        if (outer <= 0)
            return;

        var (ring, disc) = DrawMode switch
        {
            DrawModeEnum.Pressed => (_palette.Accent, _palette.Accent),
            DrawModeEnum.Hover => (_palette.Accent, _palette.Text),
            DrawModeEnum.Disabled => (_palette.Border, _palette.Muted),
            _ => (_palette.BorderBright, _palette.Text),
        };

        var inner = outer - (RingWidth + RingGap) * UIScale;
        if (inner <= 0)
            return;

        handle.DrawCircle(center, outer, ring);
        handle.DrawCircle(center, outer - RingWidth * UIScale, _palette.Panel);
        handle.DrawCircle(center, inner, disc);
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A pressure vessel drawn as the dial it would actually be.
///
///     A bar would read the same as the charge meter next to it, and the two are not the
///     same kind of number — charge drains steadily, pressure jumps when the compressor
///     catches a lungful. A needle makes that difference visible, and a dial with a red
///     arc tells you where empty is without a legend.
/// </summary>
public sealed class PressureGauge : Control
{
    /// <summary>Fill fraction, 0 to 1.</summary>
    public float Fraction { get; set; }

    /// <summary>Nothing installed at all — the dial reads dead rather than empty.</summary>
    public bool Present { get; set; }

    /// <summary>Compressor is pulling gas in; the needle gets a live tint.</summary>
    public bool Pumping { get; set; }

    /// <summary>Fraction below which the arc is drawn as a warning.</summary>
    public float LowFraction { get; set; } = 0.25f;

    public Color DialColor { get; set; } = IS14Palette.Panel;
    public Color RimColor { get; set; } = IS14Palette.Border;
    public Color HubColor { get; set; } = IS14Palette.BorderBright;
    public Color LowColor { get; set; } = IS14Palette.Bad;
    public Color ActiveColor { get; set; } = IS14Palette.Accent;
    public Color FullColor { get; set; } = IS14Palette.Good;

    private const float StartAngle = MathF.PI * 0.78f;
    private const float SweepAngle = MathF.PI * 1.44f;
    private const int ArcSegments = 48;

    public PressureGauge()
    {
        MinSize = new Vector2(74, 74);
        MouseFilter = MouseFilterMode.Ignore;
    }

    public Color NeedleColor => !Present
        ? RimColor
        : Fraction <= LowFraction
            ? LowColor
            : Pumping
                ? ActiveColor
                : FullColor;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        var centre = new Vector2(box.Left + box.Width / 2f, box.Top + box.Height / 2f);
        var radius = MathF.Min(box.Width, box.Height) / 2f - 2f;

        if (radius <= 4f)
            return;

        // Face and bezel.
        handle.DrawCircle(centre, radius, DialColor);
        DrawArc(handle, centre, radius, 0f, MathF.Tau, RimColor, 1f, 64);

        // The scale, with the low end called out.
        var low = Math.Clamp(LowFraction, 0f, 1f);
        DrawArc(handle, centre, radius - 5f, StartAngle, SweepAngle * low, LowColor.WithAlpha(0.55f), 3f);
        DrawArc(handle, centre, radius - 5f, StartAngle + SweepAngle * low, SweepAngle * (1f - low),
            RimColor, 3f);

        // Filled portion.
        var fill = Present ? Math.Clamp(Fraction, 0f, 1f) : 0f;

        if (fill > 0f)
            DrawArc(handle, centre, radius - 5f, StartAngle, SweepAngle * fill, NeedleColor, 3f);

        // Needle.
        var angle = StartAngle + SweepAngle * fill;
        var tip = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius - 9f);

        handle.DrawLine(centre, tip, NeedleColor);
        handle.DrawCircle(centre, 3f, HubColor);
    }

    /// <summary>
    ///     Robust has no arc primitive, so the scale is walked as short segments. At this
    ///     size the seams are invisible and it costs nothing.
    /// </summary>
    private static void DrawArc(
        DrawingHandleScreen handle,
        Vector2 centre,
        float radius,
        float from,
        float sweep,
        Color color,
        float thickness,
        int segments = ArcSegments)
    {
        if (sweep <= 0f || radius <= 0f)
            return;

        var steps = Math.Max(2, (int) (segments * (sweep / MathF.Tau)) + 2);
        var previous = centre + new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius;

        for (var i = 1; i <= steps; i++)
        {
            var angle = from + sweep * (i / (float) steps);
            var point = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

            for (var t = 0; t < Math.Max(1, (int) thickness); t++)
            {
                var offset = t * 0.6f;
                var inner = centre + (previous - centre).Normalized() * (radius - offset);
                var innerNext = centre + (point - centre).Normalized() * (radius - offset);
                handle.DrawLine(inner, innerNext, color);
            }

            previous = point;
        }
    }
}

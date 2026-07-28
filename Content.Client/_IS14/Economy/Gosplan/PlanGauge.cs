// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Economy.Gosplan;

/// <summary>
/// The board's meter. Unlike a plain progress bar the scale runs past 100% all the
/// way to the payout cap, so overfulfillment is something you can actually see, and
/// the failure line is drawn on the track itself — a department can tell at a glance
/// how far it is from a sanction.
/// </summary>
public sealed class PlanGauge : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Fulfillment being shown, uncapped. Anything past <see cref="MaxValue"/> is clamped.</summary>
    public float Value { get; set; }

    /// <summary>Right-hand end of the scale. Defaults to the usual payout cap.</summary>
    public float MaxValue { get; set; } = 1.5f;

    /// <summary>Where the 100% line is drawn, or null for a plain scale.</summary>
    public float? Target { get; set; } = 1f;

    /// <summary>Everything left of this is tinted as the failure zone.</summary>
    public float? Fail { get; set; }

    /// <summary>Number of cells the track is cut into. Zero draws a smooth bar.</summary>
    public int Segments { get; set; } = 15;

    /// <summary>Breathes the fill in and out — used when the period is about to be scored.</summary>
    public bool Pulse { get; set; }

    public Color FillColor { get; set; } = GosplanPalette.Good;

    /// <summary>Fill drawn to the right of the target line.</summary>
    public Color OverColor { get; set; } = GosplanPalette.Over;

    public PlanGauge()
    {
        IoCManager.InjectDependencies(this);
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = PixelSizeBox;
        var width = box.Width;
        var height = box.Height;

        if (width <= 0f || height <= 0f)
            return;

        // Everything is drawn in pixels, so hairlines have to follow the UI scale or
        // they vanish on high-DPI displays.
        var line = MathF.Max(1f, MathF.Round(UIScale));
        var max = MathF.Max(0.01f, MaxValue);

        float ToX(float value) => Math.Clamp(value / max, 0f, 1f) * width;

        handle.DrawRect(box, GosplanPalette.Track);

        if (Fail is { } fail && fail > 0f)
            handle.DrawRect(new UIBox2(0f, 0f, ToX(fail), height), GosplanPalette.FailZone);

        var fillEnd = ToX(Value);
        var targetX = Target is { } target ? ToX(target) : width;

        var fill = FillColor;
        var over = OverColor;

        if (Pulse)
        {
            var wave = 0.78f + 0.22f * MathF.Sin((float)_timing.RealTime.TotalSeconds * 5f);
            fill = fill.WithAlpha(wave);
            over = over.WithAlpha(wave);
        }

        if (fillEnd > 0f)
        {
            handle.DrawRect(new UIBox2(0f, 0f, MathF.Min(fillEnd, targetX), height), fill);

            // Past the target the bar changes colour instead of stopping, which is the
            // whole point of leaving room to the right of the 100% line.
            if (fillEnd > targetX)
                handle.DrawRect(new UIBox2(targetX, 0f, fillEnd, height), over);
        }

        // Cell separators, punched through the fill to keep the indicator look.
        if (Segments > 1)
        {
            for (var i = 1; i < Segments; i++)
            {
                var x = width * i / Segments;
                handle.DrawRect(new UIBox2(x, 0f, x + line, height), GosplanPalette.Segment);
            }
        }

        if (Target != null)
        {
            var markerWidth = line * 2f;
            var x = Math.Clamp(targetX - markerWidth * 0.5f, 0f, width - markerWidth);
            handle.DrawRect(new UIBox2(x, 0f, x + markerWidth, height), GosplanPalette.Gold);
        }

        if (Fail is { } failLine && failLine > 0f)
        {
            var x = Math.Clamp(ToX(failLine) - line * 0.5f, 0f, width - line);
            handle.DrawRect(new UIBox2(x, 0f, x + line, height), GosplanPalette.Failing);
        }

        DrawBorder(handle, box, line, GosplanPalette.Border);
    }

    private static void DrawBorder(DrawingHandleScreen handle, UIBox2 box, float line, Color color)
    {
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + line), color);
        handle.DrawRect(new UIBox2(box.Left, box.Bottom - line, box.Right, box.Bottom), color);
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + line, box.Bottom), color);
        handle.DrawRect(new UIBox2(box.Right - line, box.Top, box.Right, box.Bottom), color);
    }
}

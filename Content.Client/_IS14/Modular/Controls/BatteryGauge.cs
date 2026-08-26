// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Modular.Controls;

/// <summary>
///     Charge drawn as an actual cell — casing, terminal and a row of bars — rather than a
///     progress bar with a number stapled to it. It is the one readout a suit wearer checks
///     mid-fight, so it has to be legible from the corner of the eye.
/// </summary>
public sealed class BatteryGauge : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int Segments = 10;

    /// <summary>Charge as a fraction of capacity.</summary>
    public float Fraction { get; set; }

    /// <summary>False when there is no core at all — the casing is drawn empty and dead.</summary>
    public bool Present { get; set; } = true;

    /// <summary>Drawing more than the core can sustain: the bars run backwards, so warn.</summary>
    public bool Draining { get; set; }

    public BatteryGauge()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(148, 52);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        var scale = UIScale;

        var terminalWidth = 5f * scale;
        var border = MathF.Max(1f, MathF.Round(2f * scale));
        var inset = MathF.Max(1f, MathF.Round(3f * scale));

        var body = new UIBox2(box.Left, box.Top, box.Right - terminalWidth, box.Bottom);

        // Casing.
        var casing = Present ? ChassisStyle.BorderBright : ChassisStyle.Border;
        handle.DrawRect(body, casing);
        handle.DrawRect(Deflate(body, border), ChassisStyle.Backdrop);

        // Terminal nub on the right, so it reads as a cell and not as a bar.
        var nubHeight = body.Height * 0.34f;
        var nubTop = body.Top + (body.Height - nubHeight) / 2f;
        handle.DrawRect(new UIBox2(body.Right, nubTop, box.Right, nubTop + nubHeight), casing);

        var inner = Deflate(body, border + inset);
        if (inner.Width <= 0 || inner.Height <= 0)
            return;

        if (!Present)
        {
            // No core: a dead cell, struck through.
            var mid = inner.Top + inner.Height / 2f;
            var bar = MathF.Max(1f, 2f * scale);
            handle.DrawRect(new UIBox2(inner.Left, mid - bar / 2f, inner.Right, mid + bar / 2f), ChassisStyle.Bad);
            return;
        }

        var fraction = Math.Clamp(Fraction, 0f, 1f);
        var color = fraction switch
        {
            <= 0.15f => ChassisStyle.Bad,
            <= 0.4f => ChassisStyle.Warn,
            _ => ChassisStyle.Good,
        };

        // A dying cell blinks. Nothing else on the readout moves, so it catches the eye.
        if (fraction <= 0.15f || Draining)
        {
            var pulse = 0.55f + 0.45f * MathF.Sin((float)_timing.RealTime.TotalSeconds * 6f);
            color = color.WithAlpha(pulse);
        }

        var gap = MathF.Max(1f, MathF.Round(scale));
        var cell = (inner.Width - gap * (Segments - 1)) / Segments;
        if (cell <= 0)
            return;

        var lit = fraction * Segments;

        for (var i = 0; i < Segments; i++)
        {
            var left = inner.Left + i * (cell + gap);
            var full = lit - i;

            if (full <= 0f)
            {
                // Empty cells stay visible as faint slots, so the gauge keeps its shape.
                handle.DrawRect(new UIBox2(left, inner.Top, left + cell, inner.Bottom), ChassisStyle.Panel);
                continue;
            }

            var width = full >= 1f ? cell : cell * full;
            handle.DrawRect(new UIBox2(left, inner.Top, left + cell, inner.Bottom), ChassisStyle.Panel);
            handle.DrawRect(new UIBox2(left, inner.Top, left + width, inner.Bottom), color);
        }
    }

    private static UIBox2 Deflate(UIBox2 box, float amount) => new(
        box.Left + amount,
        box.Top + amount,
        box.Right - amount,
        box.Bottom - amount);
}

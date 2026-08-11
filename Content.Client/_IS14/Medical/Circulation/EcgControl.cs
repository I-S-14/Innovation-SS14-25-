// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Medical.Circulation;

/// <summary>
/// A running heart trace on ruled paper.
/// </summary>
/// <remarks>
/// The rate is the most useful number on the readout and a number is the worst way to show it:
/// 150 means nothing at a glance, while a trace crowded with spikes means something instantly.
/// <para>
/// The complex is laid out in <em>absolute seconds</em>, not as a fraction of the beat. That is
/// how a real heart works — the QRS takes about the same tenth of a second whatever the rate,
/// and what shortens as the rate climbs is the flat stretch between beats. Doing it the other
/// way stretches and squashes the spike itself, which both looks wrong and, at high rates,
/// makes the spike narrower than one sample so it flickers or vanishes entirely.
/// </para>
/// </remarks>
public sealed class EcgControl : Control
{
    /// <summary>Samples across the strip. High enough to resolve a spike a hundredth wide.</summary>
    private const int Samples = 480;

    /// <summary>How many seconds of trace the strip shows.</summary>
    private const float SecondsOnScreen = 4f;

    /// <summary>Paper squares per second, as on real ECG paper.</summary>
    private const float GridPerSecond = 5f;

    private readonly float[] _trace = new float[Samples];

    /// <summary>Seconds since the last beat started.</summary>
    private float _sinceBeat;

    /// <summary>Fractional sample left over from the last frame, so slow frames do not drift.</summary>
    private float _carry;

    /// <summary>Total samples printed, so the paper's ruling can move with the trace.</summary>
    private float _printed;

    /// <summary>Per-beat jitter, redrawn each beat: real hearts are not metronomes.</summary>
    private float _beatScale = 1f;
    private float _beatSkew;

    /// <summary>Slow wander of the baseline, the way breathing moves a real trace.</summary>
    private float _wander;

    private readonly System.Random _random = new();

    /// <summary>Beats per minute the trace is currently drawing.</summary>
    public float HeartRate { get; set; } = 70f;

    /// <summary>Colour of the trace. The panel sets this from the patient's shock stage.</summary>
    public Color TraceColor { get; set; } = Color.FromHex("#3ABB6A");

    /// <summary>Whether the heart is beating at all. A stopped one draws a flat line.</summary>
    public bool Beating { get; set; } = true;

    public EcgControl()
    {
        MinSize = new Vector2(0, 60);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // The paper always moves at the same speed. Only the number of complexes printed on it
        // changes with the rate — which is the whole point of ruled paper.
        var perSecond = Samples / SecondsOnScreen;
        var advance = perSecond * args.DeltaSeconds + _carry;
        var steps = (int) advance;
        _carry = advance - steps;

        if (steps <= 0)
            return;

        steps = Math.Min(steps, Samples);

        var secondsPerSample = 1f / perSecond;
        var interval = Beating && HeartRate > 1f ? 60f / HeartRate : float.PositiveInfinity;

        Array.Copy(_trace, steps, _trace, 0, Samples - steps);
        _printed += steps;

        for (var i = Samples - steps; i < Samples; i++)
        {
            _sinceBeat += secondsPerSample;

            if (_sinceBeat >= interval)
            {
                _sinceBeat -= interval;

                // New beat, new small imperfections. Amplitude varies a little with breathing
                // and the complex sits a hair early or late — without this the strip is a
                // repeating texture, which is the one thing a real one never looks like.
                _beatScale = 0.9f + _random.NextSingle() * 0.2f;
                _beatSkew = (_random.NextSingle() - 0.5f) * 0.012f;
            }

            // Baseline wander, plus a little needle noise on top.
            _wander += (_random.NextSingle() - 0.5f) * 0.004f;
            _wander = Math.Clamp(_wander * 0.995f, -0.03f, 0.03f);

            var noise = (_random.NextSingle() - 0.5f) * 0.012f;

            _trace[i] = Beating
                ? Sample(_sinceBeat + _beatSkew) * _beatScale + _wander + noise
                : _wander * 0.4f + noise * 0.5f;
        }
    }

    /// <summary>
    /// One point of a PQRST complex, in seconds after the beat began.
    /// </summary>
    /// <remarks>
    /// Gaussians rather than line segments: the atrial bump, the tall narrow spike with its
    /// dips either side and the broad recovery wave all have different widths, and widths are
    /// exactly what line segments make fiddly. Offsets and widths are real ones — the whole
    /// complex runs about four tenths of a second, so above 150 or so the waves start to crowd
    /// into each other, which is what a racing heart genuinely looks like on paper.
    /// </remarks>
    private static float Sample(float t)
    {
        return Bump(t, 0.100f, 0.022f) * 0.13f   // P
             - Bump(t, 0.188f, 0.008f) * 0.11f   // Q
             + Bump(t, 0.200f, 0.009f) * 1.00f   // R
             - Bump(t, 0.216f, 0.011f) * 0.24f   // S
             + Bump(t, 0.340f, 0.045f) * 0.26f;  // T
    }

    private static float Bump(float x, float centre, float width)
    {
        var d = (x - centre) / width;
        return MathF.Exp(-0.5f * d * d);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;

        if (box.Width <= 1 || box.Height <= 1)
            return;

        handle.DrawRect(box, Background);

        var mid = box.Top + box.Height * 0.62f;

        DrawGrid(handle, box, mid, _printed * ((float) box.Width / (Samples - 1)));

        var step = (float) box.Width / (Samples - 1);
        var amplitude = box.Height * 0.42f;
        var previous = new Vector2(box.Left, mid - _trace[0] * amplitude);

        for (var i = 1; i < Samples; i++)
        {
            var point = new Vector2(box.Left + step * i, mid - _trace[i] * amplitude);
            DrawThick(handle, previous, point);
            previous = point;
        }
    }

    /// <summary>
    /// Ruled paper: fine squares with a heavier line every fifth, the way a real strip is
    /// printed. It is not decoration — the squares are what let somebody read a rate off the
    /// spacing rather than off the label.
    /// </summary>
    private static void DrawGrid(DrawingHandleScreen handle, UIBox2i box, float mid, float scrolled)
    {
        var spacing = box.Width / (SecondsOnScreen * GridPerSecond);

        if (spacing <= 0f)
            return;

        // Each rule is drawn at its own absolute index on the roll rather than at a screen slot
        // with an offset. Mixing those two was what made the heavy line hop between rules every
        // few squares: the offset wrapped on one period and the heavy test counted on another.
        var first = (int) MathF.Floor(scrolled / spacing);

        for (var n = first; (n * spacing) - scrolled <= box.Width; n++)
        {
            var x = box.Left + (n * spacing - scrolled);

            if (x < box.Left || x > box.Right)
                continue;

            handle.DrawLine(
                new Vector2(x, box.Top),
                new Vector2(x, box.Bottom),
                n % 5 == 0 ? GridBold : GridFine);
        }

        // Horizontal rules are hung off the baseline so the trace sits on a line rather than
        // between two of them.
        for (var i = -6; i <= 6; i++)
        {
            var y = mid + i * spacing;

            if (y < box.Top || y > box.Bottom)
                continue;

            handle.DrawLine(
                new Vector2(box.Left, y),
                new Vector2(box.Right, y),
                i % 5 == 0 ? GridBold : GridFine);
        }
    }

    /// <summary>
    /// Draws one segment as a few stacked lines.
    /// </summary>
    /// <remarks>
    /// The draw handle has no line width, so thickness is three passes a pixel apart. Cheap,
    /// and on a trace this dense it is indistinguishable from a real stroked line.
    /// </remarks>
    private void DrawThick(DrawingHandleScreen handle, Vector2 from, Vector2 to)
    {
        for (var offset = -1; offset <= 1; offset++)
        {
            var shift = new Vector2(0, offset);
            handle.DrawLine(from + shift, to + shift, TraceColor);
        }
    }

    private static readonly Color Background = Color.FromHex("#05100A");
    private static readonly Color GridFine = Color.FromHex("#123021");
    private static readonly Color GridBold = Color.FromHex("#1D5136");
}

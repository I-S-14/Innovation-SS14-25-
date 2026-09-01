// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A slider that reads as a piece of instrumentation rather than a web form: a notched
///     track, a machined handle and the value stamped on it.
///
///     Drawn rather than assembled from style boxes because the engine slider is three
///     nested panels whose look is fixed by the stylesheet — matching a dark hardware
///     palette meant fighting it. Here every colour is a property.
///
///     Reports on release rather than continuously. Anything driven by a slider that sends
///     over the network — a suit module's setting, a machine's target — wants one message
///     when the player is done, not one per pixel of the drag.
/// </summary>
public sealed class IS14Slider : Control
{
    private const float TrackHeight = 6f;
    private const float HandleWidth = 7f;
    private const float HandleHeight = 20f;

    private bool _grabbed;
    private float _value;

    /// <summary>Fires while dragging, for live readouts next to the control.</summary>
    public event Action<float>? OnValueChanged;

    /// <summary>
    ///     Fires when the handle is taken hold of. Anything that rebuilds the surrounding
    ///     UI on a timer or a network push should stand still until <see cref="OnReleased"/>,
    ///     or it will destroy this control mid-drag.
    /// </summary>
    public event Action? OnGrabbed;

    /// <summary>Fires once, when the handle is let go. This is the one to act on.</summary>
    public event Action<float>? OnReleased;

    public float MinValue { get; set; }
    public float MaxValue { get; set; } = 1f;

    /// <summary>
    ///     Rounds the value as it is dragged. Zero means continuous. A dial that only
    ///     makes sense in whole numbers should say so by refusing to sit between them.
    /// </summary>
    public float Step { get; set; }

    /// <summary>
    ///     Notches drawn along the track, not counting the ends. Purely decorative — they
    ///     give the eye something to measure against and make the thing read as hardware.
    /// </summary>
    public int Notches { get; set; } = 8;

    public Color TrackColor { get; set; } = IS14Palette.Backdrop;
    public Color TrackBorderColor { get; set; } = IS14Palette.Border;
    public Color FillColor { get; set; } = IS14Palette.Accent;
    public Color NotchColor { get; set; } = IS14Palette.Border;
    public Color HandleColor { get; set; } = IS14Palette.Text;
    public Color HandleEdgeColor { get; set; } = IS14Palette.Backdrop;

    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, MinValue, MaxValue);

            if (MathHelper.CloseTo(clamped, _value))
                return;

            _value = clamped;
            OnValueChanged?.Invoke(_value);
        }
    }

    /// <summary>Where the handle sits, 0 to 1.</summary>
    private float Ratio => MaxValue > MinValue
        ? Math.Clamp((_value - MinValue) / (MaxValue - MinValue), 0f, 1f)
        : 0f;

    public IS14Slider()
    {
        MinSize = new Vector2(0, HandleHeight + 4);
        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = true;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _grabbed = true;
        OnGrabbed?.Invoke();
        SetFromPointer(args.RelativePosition.X);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_grabbed)
            return;

        _grabbed = false;
        OnReleased?.Invoke(_value);
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_grabbed)
            SetFromPointer(args.RelativePosition.X);
    }

    /// <summary>
    ///     Losing the pointer mid-drag has to count as letting go, or the value the player
    ///     dragged to would never be sent.
    /// </summary>
    protected override void MouseExited()
    {
        base.MouseExited();

        if (!_grabbed)
            return;

        _grabbed = false;
        OnReleased?.Invoke(_value);
    }

    private void SetFromPointer(float x)
    {
        var usable = MathF.Max(1f, Size.X - HandleWidth);
        var ratio = Math.Clamp((x - HandleWidth / 2f) / usable, 0f, 1f);
        var raw = MinValue + (MaxValue - MinValue) * ratio;

        Value = Step > 0f ? MathF.Round(raw / Step) * Step : raw;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = PixelSizeBox;
        var scale = UIScale;
        var centre = (box.Top + box.Bottom) / 2f;

        var half = TrackHeight * scale / 2f;
        var handleHalf = HandleWidth * scale / 2f;

        // The track stops short of the edges so the handle never hangs off the control.
        var left = box.Left + handleHalf;
        var right = box.Right - handleHalf;
        var track = new UIBox2(left, centre - half, right, centre + half);

        handle.DrawRect(track, TrackColor);
        handle.DrawRect(track, TrackBorderColor, filled: false);

        var span = right - left;
        var handleX = left + span * Ratio;

        if (handleX > left)
            handle.DrawRect(new UIBox2(left, track.Top, handleX, track.Bottom), FillColor);

        // Notches sit above the track rather than across it: drawn through the fill they
        // would read as segments of the value, which is exactly what they are not.
        if (Notches > 0)
        {
            var width = MathF.Max(1f, MathF.Round(scale));
            var top = track.Top - 4f * scale;

            for (var i = 1; i <= Notches; i++)
            {
                var x = left + span * ((float) i / (Notches + 1));
                handle.DrawRect(new UIBox2(x, top, x + width, track.Top - scale), NotchColor);
            }
        }

        var handleHeight = HandleHeight * scale / 2f;
        var body = new UIBox2(handleX - handleHalf, centre - handleHeight, handleX + handleHalf, centre + handleHeight);

        handle.DrawRect(body, HandleEdgeColor);
        handle.DrawRect(new UIBox2(body.Left + scale, body.Top + scale, body.Right - scale, body.Bottom - scale), HandleColor);

        // A groove down the middle of the handle: it is what makes it read as a machined
        // part rather than a rectangle.
        var groove = MathF.Max(1f, MathF.Round(scale));
        var grooveX = handleX - groove / 2f;
        handle.DrawRect(
            new UIBox2(grooveX, body.Top + 4f * scale, grooveX + groove, body.Bottom - 4f * scale),
            HandleEdgeColor);
    }
}

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._IS14.Controls;

/// <summary>
///     An invisible sheet that turns a click-and-drag into a stream of deltas, in virtual
///     pixels. Lay it over anything that should be draggable without that thing having to know
///     about input — a viewport, a map, a canvas.
///
///     It swallows the click, which is the point: a drag inside a window must not also drag the
///     window, and a drag over a viewport must not poke the world behind it.
/// </summary>
public sealed class DragSurface : Control
{
    private bool _dragging;

    /// <summary>Movement since the last event, in virtual pixels.</summary>
    public event Action<Vector2>? OnDragged;

    public DragSurface()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = false;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        // The UI keeps sending us moves while the button is held, even past our own edge, so a
        // drag does not die the moment the cursor leaves a small control.
        if (_dragging)
            OnDragged?.Invoke(args.Relative);
    }
}

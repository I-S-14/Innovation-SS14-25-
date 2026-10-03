// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._IS14.Research;

/// <summary>
/// The research map: nodes placed by their prototype position, lines from every technology back
/// to its prerequisites, and the whole thing dragged around with the mouse.
/// </summary>
/// <remarks>
/// Panning instead of two scrollbars: a branch spreads in every direction from its root, so
/// scrollbars meant hunting for the thumb every time the eye moved. Dragging is also what the
/// Goob console does, so the gesture is already familiar.
/// </remarks>
public sealed class IS14ResearchMap : LayoutContainer
{
    private static readonly Color DoneColor = Color.FromHex("#5ec45e");
    private static readonly Color PendingColor = Color.FromHex("#4a4a55");

    /// <summary>Node positions before panning, so a drag never accumulates rounding errors.</summary>
    private readonly Dictionary<Control, Vector2> _basePositions = new();

    /// <summary>Zoom bounds. Below this the icons stop reading, above it nothing fits.</summary>
    public const float MinZoom = 0.4f;

    public const float MaxZoom = 1.6f;

    private Vector2 _offset;
    private Vector2 _contentSize;
    private bool _dragging;
    private bool _centered;
    private float _zoom = 1f;

    /// <summary>Raised when the wheel changes the zoom, so the slider can follow it.</summary>
    public event Action<float>? OnZoomChanged;

    public float Zoom => _zoom;

    public IS14ResearchMap()
    {
        // Pass, not Stop: presses on empty space reach the map and start a drag, while presses
        // on a node still belong to that node's button.
        MouseFilter = MouseFilterMode.Pass;
        RectClipContent = true;
    }

    /// <summary>Adds a node at its map position, in pixels relative to the branch's top-left.</summary>
    public void Place(Control child, Vector2 position)
    {
        AddChild(child);
        _basePositions[child] = position;

        _contentSize = Vector2.Max(_contentSize, position + new Vector2(IS14ResearchNode.Size, IS14ResearchNode.Size));

        ApplyLayout();
    }

    /// <summary>
    /// Sets the zoom, keeping a point of the viewport where it is. With no anchor given the
    /// centre of the viewport holds still, which is what the slider wants; the wheel passes
    /// the cursor, so the map grows around whatever is under it.
    /// </summary>
    public void SetZoom(float zoom, Vector2? anchor = null)
    {
        var next = Math.Clamp(zoom, MinZoom, MaxZoom);

        if (MathHelper.CloseTo(next, _zoom))
            return;

        var focus = anchor ?? Size / 2f;

        // The point under `focus` must map to the same place after the change.
        _offset = focus - (focus - _offset) * (next / _zoom);
        _zoom = next;

        ApplyLayout();
    }

    /// <summary>Puts the branch back in the middle of the viewport.</summary>
    public void Center()
    {
        _offset = (Size - _contentSize * _zoom) / 2f;
        ApplyLayout();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0f)
            return;

        args.Handle();

        SetZoom(_zoom * (1f + args.Delta.Y * 0.1f), args.RelativePosition);
        OnZoomChanged?.Invoke(_zoom);
    }

    /// <summary>Moves the map if a drag is in progress. Called by the window for every mouse move.</summary>
    public void Pan(Vector2 delta)
    {
        if (!_dragging)
            return;

        _offset += delta;
        ApplyLayout();
    }

    public void StopDrag()
    {
        _dragging = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.Use || args.Function == EngineKeyFunctions.UIRightClick)
            _dragging = true;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.Use || args.Function == EngineKeyFunctions.UIRightClick)
            _dragging = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!_centered && Size.X > 0 && Size.Y > 0)
        {
            Center();
            _centered = true;
        }

        foreach (var child in Children)
        {
            if (child is not IS14ResearchNode node || node.Prototype.TechnologyPrerequisites.Count == 0)
                continue;

            foreach (var other in Children)
            {
                if (other is not IS14ResearchNode prerequisite)
                    continue;

                if (!node.Prototype.TechnologyPrerequisites.Contains(prerequisite.Prototype.ID))
                    continue;

                DrawLink(handle, prerequisite, node);
            }
        }

        base.Draw(handle);
    }

    /// <summary>
    /// Straight line between the two nodes' centres. Branches spread in every direction, so an
    /// elbow would cross other nodes; the nodes themselves are opaque and cover the ends.
    /// </summary>
    private void DrawLink(DrawingHandleScreen handle, IS14ResearchNode from, IS14ResearchNode to)
    {
        var color = from.State == IS14NodeState.Researched ? DoneColor : PendingColor;

        var start = new Vector2(
            from.PixelPosition.X + from.PixelWidth / 2f,
            from.PixelPosition.Y + from.PixelHeight / 2f);

        var end = new Vector2(
            to.PixelPosition.X + to.PixelWidth / 2f,
            to.PixelPosition.Y + to.PixelHeight / 2f);

        handle.DrawLine(start, end, color);
    }

    /// <summary>
    /// Places every node for the current pan and zoom. Nodes are resized rather than drawn
    /// through a transform, so their icons and tooltips keep working at any scale.
    /// </summary>
    private void ApplyLayout()
    {
        var size = new Vector2(IS14ResearchNode.Size * _zoom, IS14ResearchNode.Size * _zoom);

        foreach (var (child, position) in _basePositions)
        {
            child.SetSize = size;
            LayoutContainer.SetPosition(child, position * _zoom + _offset);
        }
    }
}

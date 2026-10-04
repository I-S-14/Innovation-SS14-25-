// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._IS14.Research.Editor;

/// <summary>
/// The canvas the tree is built on: nodes on a grid, links drawn from their prerequisites, the
/// view panned and zoomed, and the selected node dragged from cell to cell.
/// </summary>
/// <remarks>
/// A sibling of the console's map rather than a reuse of it. The console draws what the station
/// bought; this one has to let a node move, snap it to a cell and report where it landed, and it
/// never dims anything — an unfinished tree is not a locked one.
/// </remarks>
public sealed class IS14EditorMap : LayoutContainer
{
    public const float MinZoom = 0.3f;

    public const float MaxZoom = 1.6f;

    private static readonly Color GridColor = Color.FromHex("#2a2a30");
    private static readonly Color LinkColor = Color.FromHex("#4a4a55");
    private static readonly Color DragColor = Color.FromHex("#e8c547");

    private readonly Dictionary<IS14EditorNode, Vector2i> _cells = new();

    private Vector2 _offset;
    private float _zoom = 1f;
    private bool _panning;
    private IS14EditorNode? _dragging;

    /// <summary>Cell the drag started from, and how far the cursor has travelled since.</summary>
    private Vector2i _dragFrom;
    private Vector2 _dragTravel;
    private bool _centered;

    /// <summary>A node was dropped on a new cell.</summary>
    public event Action<IS14EditorNode, Vector2i>? OnNodeMoved;

    /// <summary>Empty space was clicked: the editor drops the selection.</summary>
    public event Action? OnBackgroundPressed;

    public event Action<float>? OnZoomChanged;

    public float Zoom => _zoom;

    /// <summary>Tells the map which prerequisites to draw links for.</summary>
    public Func<IS14EditorNode, IReadOnlyList<string>>? Prerequisites;

    public IS14EditorMap()
    {
        MouseFilter = MouseFilterMode.Pass;
        RectClipContent = true;
    }

    public void Place(IS14EditorNode node, Vector2i cell)
    {
        AddChild(node);
        _cells[node] = cell;
        ApplyLayout();
    }

    public void Remove(IS14EditorNode node)
    {
        _cells.Remove(node);
        RemoveChild(node);
    }

    public void ClearNodes()
    {
        foreach (var node in _cells.Keys.ToList())
        {
            RemoveChild(node);
        }

        _cells.Clear();
    }

    public void SetCell(IS14EditorNode node, Vector2i cell)
    {
        _cells[node] = cell;
        ApplyLayout();
    }

    /// <summary>Starts dragging a node; the window feeds moves in through <see cref="Drag"/>.</summary>
    public void BeginDrag(IS14EditorNode node)
    {
        if (!_cells.TryGetValue(node, out var cell))
            return;

        _dragging = node;
        _dragFrom = cell;
        _dragTravel = Vector2.Zero;
    }

    /// <summary>Mouse moved: pans the view, or carries the node that is being dragged.</summary>
    public void Drag(Vector2 delta)
    {
        if (_dragging != null)
        {
            // The travel is accumulated in pixels and only then turned into cells. Rounding the
            // cell on every mouse move instead would throw away each small delta — a three-pixel
            // nudge rounds back to the cell it came from — and the node would sit still until one
            // single event happened to cross half a cell.
            _dragTravel += delta / _zoom;

            var cell = _dragFrom + new Vector2i(
                (int) MathF.Round(_dragTravel.X / IS14EditorNode.ColumnStride),
                (int) MathF.Round(_dragTravel.Y / IS14EditorNode.RowStride));

            if (cell == _cells[_dragging])
                return;

            _cells[_dragging] = cell;
            ApplyLayout();
            return;
        }

        if (!_panning)
            return;

        _offset += delta;
        ApplyLayout();
    }

    public void StopDrag()
    {
        _panning = false;

        if (_dragging == null)
            return;

        var node = _dragging;

        _dragging = null;
        _dragTravel = Vector2.Zero;

        OnNodeMoved?.Invoke(node, _cells[node]);
    }

    public void SetZoom(float zoom, Vector2? anchor = null)
    {
        var next = Math.Clamp(zoom, MinZoom, MaxZoom);

        if (MathHelper.CloseTo(next, _zoom))
            return;

        var focus = anchor ?? Size / 2f;

        _offset = focus - (focus - _offset) * (next / _zoom);
        _zoom = next;

        ApplyLayout();
    }

    /// <summary>Puts the cell the tree starts from back in the middle of the viewport.</summary>
    public void Center()
    {
        if (_cells.Count == 0)
        {
            _offset = Size / 2f;
            ApplyLayout();
            _centered = true;
            return;
        }

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);

        foreach (var cell in _cells.Values)
        {
            var position = CellPosition(cell);
            min = Vector2.Min(min, position);
            max = Vector2.Max(max, position + new Vector2(IS14EditorNode.Size));
        }

        _offset = Size / 2f - (min + max) / 2f * _zoom;
        ApplyLayout();
        _centered = true;
    }

    public void CenterOn(IS14EditorNode node)
    {
        if (!_cells.TryGetValue(node, out var cell) || Size.X <= 0 || Size.Y <= 0)
            return;

        var half = new Vector2(IS14EditorNode.Size) * _zoom / 2f;

        _offset = Size / 2f - (CellPosition(cell) * _zoom + half);
        ApplyLayout();
        _centered = true;
    }

    /// <summary>Map cell under the cursor, used when a new node is dropped onto the canvas.</summary>
    public Vector2i CellAt(Vector2 relative)
    {
        var position = (relative - _offset) / _zoom;

        return new Vector2i(
            (int) MathF.Round(position.X / IS14EditorNode.ColumnStride),
            (int) MathF.Round(position.Y / IS14EditorNode.RowStride));
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

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            // The click reached the canvas, so it missed every node.
            _panning = true;
            OnBackgroundPressed?.Invoke();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            _panning = true;
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.UIRightClick)
            StopDrag();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!_centered && Size.X > 0 && Size.Y > 0)
            Center();

        DrawGrid(handle);

        foreach (var (node, _) in _cells)
        {
            var prereqs = Prerequisites?.Invoke(node);

            if (prereqs == null)
                continue;

            foreach (var (other, _) in _cells)
            {
                if (!prereqs.Contains(other.Node.Id))
                    continue;

                handle.DrawLine(Centre(other), Centre(node), node == _dragging ? DragColor : LinkColor);
            }
        }

        base.Draw(handle);
    }

    /// <summary>
    /// The cell grid, so dropping a node somewhere sensible is possible without guessing. Drawn
    /// only while zoomed in enough for the lines to mean anything.
    /// </summary>
    private void DrawGrid(DrawingHandleScreen handle)
    {
        if (_zoom < 0.6f)
            return;

        var stepX = IS14EditorNode.ColumnStride * _zoom;
        var stepY = IS14EditorNode.RowStride * _zoom;

        var startX = PixelPosition.X + _offset.X % stepX;
        var startY = PixelPosition.Y + _offset.Y % stepY;

        for (var x = startX; x < PixelPosition.X + PixelWidth; x += stepX)
        {
            handle.DrawLine(new Vector2(x, PixelPosition.Y),
                new Vector2(x, PixelPosition.Y + PixelHeight), GridColor);
        }

        for (var y = startY; y < PixelPosition.Y + PixelHeight; y += stepY)
        {
            handle.DrawLine(new Vector2(PixelPosition.X, y),
                new Vector2(PixelPosition.X + PixelWidth, y), GridColor);
        }
    }

    private static Vector2 Centre(Control node)
        => new(node.PixelPosition.X + node.PixelWidth / 2f, node.PixelPosition.Y + node.PixelHeight / 2f);

    private static Vector2 CellPosition(Vector2i cell)
        => new(cell.X * IS14EditorNode.ColumnStride, cell.Y * IS14EditorNode.RowStride);

    private void ApplyLayout()
    {
        var size = new Vector2(IS14EditorNode.Size * _zoom);

        foreach (var (node, cell) in _cells)
        {
            node.SetSize = size;
            SetPosition(node, CellPosition(cell) * _zoom + _offset);
        }
    }
}

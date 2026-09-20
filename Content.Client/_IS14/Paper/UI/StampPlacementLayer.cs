// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Paper.UI;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Paper;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Robust.Client.UserInterface;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Paper.UI;

/// <summary>
///     Covers the whole page of an open document. It draws every impression the player positioned
///     by hand, and a translucent preview which follows the cursor: the stamp they are holding, or
///     a signature they asked to write. Clicking drops it where the preview sits.
/// </summary>
/// <remarks>
///     Stamps applied without a UI (faxes, mapped-in paperwork) have no position and are laid out
///     by <see cref="StampCollection"/> instead, which this layer sits on top of.
/// </remarks>
public sealed class StampPlacementLayer : Control
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    ///     Opacity of the preview stamp.
    /// </summary>
    private const float GhostAlpha = 0.45f;

    /// <summary>
    ///     How far the preview is randomly tilted from upright when it first appears, in radians.
    ///     A little wobble makes a stamped page look hand-worked instead of typeset.
    /// </summary>
    private const float TiltRange = 0.09f;

    /// <summary>
    ///     How far one notch of the scroll wheel turns the preview, in radians. Eight notches
    ///     take it from upright to the steepest angle the server will accept.
    /// </summary>
    private const float RotationStep = StampPlacementSystem.MaxRotation / 8.0f;

    /// <summary>
    ///     How much of an impression has to stay on the page, as a fraction of its own size.
    ///     The sprites do not fill their frames, so clamping by the full frame pushed stamps much
    ///     further from the edge than they looked like they were. At 0.5 an impression may hang
    ///     halfway over the edge, which reads fine; 0 lets it sit anywhere at all.
    /// </summary>
    private const float EdgeClamp = 0.5f;

    private readonly List<(StampWidget Widget, Vector2 Position)> _stamps = new();

    /// How many stamps the page carries in total, hand-placed or not.
    private int _stampCount;

    /// Cached so the per-frame preview check doesn't hit the config manager. Refreshed whenever
    /// the page is rebuilt, which is often enough to pick up a mid-round change.
    private int _maxStamps;

    private StampWidget? _ghost;
    private EntityUid? _heldStamp;
    private Vector2 _ghostPosition = new(0.5f, 0.5f);
    private float _ghostRotation;

    /// Once the player has turned a stamp themselves, stop overriding their angle with a random one.
    private bool _rotatedByPlayer;

    /// Set when the player right-clicks to put the preview away and read the page underneath.
    /// Cleared as soon as they change what they are holding.
    private bool _suspended;

    /// A signature the server offered, waiting to be placed. Takes priority over a held stamp,
    /// since the player asked for it explicitly.
    private StampDisplayInfo? _signature;

    /// <summary>
    ///     Raised with the normalized page position and the rotation the player settled on.
    /// </summary>
    public event Action<Vector2, float>? OnStampPlaced;

    /// <inheritdoc cref="OnStampPlaced"/>
    public event Action<Vector2, float>? OnSignaturePlaced;

    /// <summary>
    ///     Raised when the player waves off a signature they had been offered.
    /// </summary>
    public event Action? OnSignatureCancelled;

    public StampPlacementLayer()
    {
        IoCManager.InjectDependencies(this);

        _maxStamps = _cfg.GetCVar(IS14CVars.PaperMaxStamps);
    }

    /// <summary>
    ///     Rebuilds the page from the stamps the document currently carries. Only hand-placed
    ///     stamps get a widget here; the rest are drawn by <see cref="StampCollection"/>.
    /// </summary>
    public void SetStamps(IReadOnlyList<StampDisplayInfo> stamps)
    {
        foreach (var (widget, _) in _stamps)
        {
            RemoveChild(widget);
        }

        _stamps.Clear();
        _stampCount = stamps.Count;
        _maxStamps = _cfg.GetCVar(IS14CVars.PaperMaxStamps);

        foreach (var info in stamps)
        {
            if (info.Position is not { } position)
                continue;

            var widget = new StampWidget
            {
                StampInfo = info,
                Orientation = info.Rotation,
                MouseFilter = MouseFilterMode.Ignore,
            };

            AddChild(widget);
            _stamps.Add((widget, position));
        }

        // The page just changed, so it may have filled up.
        UpdateGhost(true);
        InvalidateArrange();
    }

    /// <summary>
    ///     Offers a signature for the player to position, in response to them using the sign verb,
    ///     or withdraws one that is no longer on the table. Driven by the UI state, so it runs on
    ///     every update and has to leave an unchanged offer alone.
    /// </summary>
    public void SetSignature(StampDisplayInfo? info)
    {
        if (info is { } offer && _stampCount < _maxStamps)
        {
            if (_signature?.StampedName == offer.StampedName)
                return;

            _signature = offer;
            _suspended = false;
            UpdateGhost(true);
            return;
        }

        if (_signature != null)
            ClearSignature();
    }

    /// <summary>
    ///     Drops a pending signature, whether it was placed or dismissed. Resets the held-item
    ///     tracking so a stamp still in hand gets its preview back on the next frame.
    /// </summary>
    private void ClearSignature()
    {
        _signature = null;
        _heldStamp = null;
        SetGhost(null);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateGhost(false);
    }

    private void UpdateGhost(bool force)
    {
        if (_signature is { } signature)
        {
            // A signature was asked for by name, so it outranks whatever happens to be in hand.
            if (force)
                SetGhost(_suspended ? null : signature);

            return;
        }

        var held = GetHeldStamp();

        if (held != _heldStamp)
        {
            // Something else is in hand now, so a previous right-click dismissal no longer applies.
            _heldStamp = held;
            _suspended = false;
        }
        else if (!force)
        {
            return;
        }

        StampDisplayInfo? info = null;
        if (!_suspended && _entMan.TryGetComponent<StampComponent>(held, out var stamp))
            info = PaperSystem.GetStampInfo(stamp);

        SetGhost(info);
    }

    /// <summary>
    ///     The stamp in the local player's active hand, if this page can still take one. Polled
    ///     rather than pushed so the preview appears and disappears as the player swaps what they
    ///     are holding, whether or not the document was opened by stamping it.
    /// </summary>
    private EntityUid? GetHeldStamp()
    {
        if (_stampCount >= _maxStamps)
            return null;

        if (_player.LocalEntity is not { } local)
            return null;

        if (!_entMan.System<SharedHandsSystem>().TryGetActiveItem(local, out var item))
            return null;

        return _entMan.HasComponent<StampComponent>(item) ? item : null;
    }

    private void SetGhost(StampDisplayInfo? pending)
    {
        if (_ghost != null)
        {
            RemoveChild(_ghost);
            _ghost = null;
        }

        if (pending is not { } info)
        {
            // Nothing to place, so let clicks through to the document underneath.
            MouseFilter = MouseFilterMode.Ignore;
            InvalidateArrange();
            return;
        }

        if (!_rotatedByPlayer)
            _ghostRotation = _random.NextFloat(-TiltRange, TiltRange);

        _ghost = new StampWidget
        {
            StampInfo = info,
            Alpha = GhostAlpha,
            Orientation = _ghostRotation,
            MouseFilter = MouseFilterMode.Ignore,
        };

        AddChild(_ghost);
        MouseFilter = MouseFilterMode.Stop;
        InvalidateArrange();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_ghost == null || Size.X <= 0 || Size.Y <= 0)
            return;

        _ghostPosition = Vector2.Clamp(args.RelativePosition / Size, Vector2.Zero, Vector2.One);
        InvalidateArrange();
    }

    /// <summary>
    ///     Shift and the scroll wheel turn the stamp. Without shift the event is left alone so the
    ///     document still scrolls, which matters on anything longer than one screen.
    /// </summary>
    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (_ghost == null || !_input.IsKeyDown(Keyboard.Key.Shift))
            return;

        _ghostRotation = Math.Clamp(_ghostRotation + args.Delta.Y * RotationStep,
            -StampPlacementSystem.MaxRotation,
            StampPlacementSystem.MaxRotation);
        _rotatedByPlayer = true;
        _ghost.Orientation = _ghostRotation;

        InvalidateArrange();
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (_ghost == null)
            return;

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            // Put the preview away for now so the page underneath can be read and clicked again.
            // A stamp comes back by swapping what is in hand, a signature by using the verb again.
            _suspended = true;

            if (_signature != null)
            {
                ClearSignature();
                OnSignatureCancelled?.Invoke();
            }
            else
            {
                SetGhost(null);
            }

            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_signature != null)
        {
            OnSignaturePlaced?.Invoke(_ghostPosition, _ghostRotation);
            ClearSignature();
        }
        else
        {
            OnStampPlaced?.Invoke(_ghostPosition, _ghostRotation);
        }

        args.Handle();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Children still need measuring so they report a sensible DesiredPixelSize, but the layer
        // is an overlay and must never push the page around.
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var (widget, position) in _stamps)
        {
            ArrangeStamp(widget, position, widget.Orientation, finalSize);
        }

        if (_ghost != null)
        {
            ArrangeStamp(_ghost, _ghostPosition, _ghostRotation, finalSize);
        }

        return finalSize;
    }

    /// <summary>
    ///     Centres a stamp on a normalized page position, keeping the whole impression on the page
    ///     even once its rotation is taken into account.
    /// </summary>
    private void ArrangeStamp(StampWidget widget, Vector2 position, float rotation, Vector2 finalSize)
    {
        var pixelSize = finalSize * UIScale;
        var half = (Vector2) widget.DesiredPixelSize * 0.5f;

        // Axis-aligned half extents of the tilted impression, scaled down by EdgeClamp so that
        // the transparent padding around the sprite does not count as part of the stamp.
        var boundsCos = half * MathF.Abs(MathF.Cos(rotation));
        var boundsSin = half * MathF.Abs(MathF.Sin(rotation));
        var bounds = new Vector2(boundsCos.X + boundsSin.Y, boundsSin.X + boundsCos.Y) * EdgeClamp;

        var min = PixelSizeBox.TopLeft + bounds;
        var max = PixelSizeBox.TopLeft + pixelSize - bounds;
        var center = Vector2.Min(Vector2.Max(PixelSizeBox.TopLeft + position * pixelSize, min), max);

        // StampWidget rotates about its own top-left corner rather than its middle, so that corner
        // has to be placed where the rotation will throw it: topLeft + R * half must land on the
        // centre we want. Offsetting by the axis-aligned extents instead is what made tilted
        // stamps drift away from the cursor.
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        var rotatedHalf = new Vector2(half.X * cos - half.Y * sin, half.X * sin + half.Y * cos);

        var topLeft = center - rotatedHalf;
        var topLeftInt = new Vector2i((int) topLeft.X, (int) topLeft.Y);
        widget.ArrangePixel(new UIBox2i(topLeftInt, topLeftInt + widget.DesiredPixelSize));
    }
}

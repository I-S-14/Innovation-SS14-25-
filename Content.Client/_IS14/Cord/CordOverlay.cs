// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._IS14.Cord;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Cord;

/// <summary>
/// Draws every attached <see cref="CordComponent"/>. The overlay itself knows nothing
/// about cables or hoses: it asks the cord's shape where the line goes and its effect
/// what colour each piece is, then draws quads. Everything interesting lives in those
/// two, which is what makes a new cord a YAML change.
/// </summary>
public sealed class CordOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;

    /// <summary>Reused between cords and between frames — this runs every frame.</summary>
    private readonly List<Vector2> _points = new();

    public CordOverlay(IEntityManager entManager, IGameTiming timing)
    {
        _entManager = entManager;
        _timing = timing;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        handle.SetTransform(Matrix3x2.Identity);

        var time = (float)_timing.RealTime.TotalSeconds;

        var cords = _entManager.EntityQueryEnumerator<CordComponent, TransformComponent>();
        while (cords.MoveNext(out var cord, out var xform))
        {
            if (cord.Anchor == null || xform.MapID != args.MapId)
                continue;

            if (!xformQuery.TryGetComponent(cord.Anchor, out var anchorXform) || anchorXform.MapID != xform.MapID)
                continue;

            DrawCord(
                handle,
                cord,
                xformSystem.GetWorldPosition(anchorXform),
                xformSystem.GetWorldPosition(xform),
                time);
        }
    }

    private void DrawCord(DrawingHandleWorld handle, CordComponent cord, Vector2 from, Vector2 to, float time)
    {
        var length = (to - from).Length();

        // Both ends in the same place: there is no line to draw, and no direction to
        // build one from either.
        if (length < 0.05f)
            return;

        _points.Clear();
        _points.Add(from);
        cord.Shape.GetPoints(new CordShapeArgs(from, to, length, cord.GetSlack(length), time), _points);

        var segments = _points.Count - 1;

        for (var i = 0; i < segments; i++)
        {
            var color = cord.Effect?.GetColor(new CordEffectArgs(cord.Color, i, segments, time, cord.Energized))
                        ?? cord.Color;

            DrawSegment(handle, _points[i], _points[i + 1], cord.Width, color);
        }
    }

    /// <summary>
    /// One straight piece, as a rotated quad rather than a hairline — <c>DrawLine</c> is
    /// a single pixel wide and most cords are not. The quad runs a little long so
    /// consecutive pieces overlap and the corners between them stay filled.
    /// </summary>
    private static void DrawSegment(DrawingHandleWorld handle, Vector2 from, Vector2 to, float width, Color color)
    {
        var delta = to - from;
        var length = delta.Length();

        if (length < 0.001f)
            return;

        var midpoint = from + delta / 2f;
        var box = new Box2(-width / 2f, -(length + width) / 2f, width / 2f, (length + width) / 2f);

        handle.DrawRect(new Box2Rotated(box.Translated(midpoint), delta.ToWorldAngle(), midpoint), color);
    }
}

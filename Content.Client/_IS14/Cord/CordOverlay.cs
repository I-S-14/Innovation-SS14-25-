// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._IS14.Cord;
using Robust.Client.GameObjects;
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

        var spriteQuery = _entManager.GetEntityQuery<SpriteComponent>();

        // Attach points are read off the sprite sheet, and how a sheet is laid onto the
        // world depends on the camera as much as on the entity — an upright sprite is
        // upright on screen, which on a turned grid is not upright in the world.
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        var cords = _entManager.EntityQueryEnumerator<CordComponent, TransformComponent>();
        while (cords.MoveNext(out var uid, out var cord, out var xform))
        {
            if (cord.Anchor is not { } anchor || xform.MapID != args.MapId)
                continue;

            if (!xformQuery.TryGetComponent(anchor, out var anchorXform) || anchorXform.MapID != xform.MapID)
                continue;

            DrawCord(
                handle,
                cord,
                xformSystem.GetWorldPosition(anchorXform)
                + Tie(anchor, anchorXform, cord.AnchorOffset, eyeRotation, xformSystem, spriteQuery),
                xformSystem.GetWorldPosition(xform)
                + Tie(uid, xform, cord.Offset, eyeRotation, xformSystem, spriteQuery),
                time);
        }
    }

    /// <summary>
    /// Turns a cord's attach point into a world-space offset by putting it through the
    /// same rotation the renderer puts the sprite through. Getting this from the entity's
    /// rotation alone is wrong for the two sprites that do not simply turn with their
    /// entity, and one of those — <c>noRot</c> — is exactly what a drip stand or a crash
    /// cart is: upright on screen whatever the grid underneath is doing, which on a
    /// turned grid is a different direction in the world every time.
    /// </summary>
    private static Vector2 Tie(
        EntityUid uid,
        TransformComponent xform,
        Vector2 offset,
        Angle eyeRotation,
        SharedTransformSystem xformSystem,
        EntityQuery<SpriteComponent> spriteQuery)
    {
        if (offset == Vector2.Zero)
            return Vector2.Zero;

        var rotation = xformSystem.GetWorldRotation(xform);

        if (spriteQuery.TryGetComponent(uid, out var sprite))
        {
            if (sprite.NoRotation)
            {
                // Drawn at -eye so that the eye matrix cancels it out and the sheet lands
                // square on the screen. Same trick, same sign, so the offset lands with it.
                rotation = -eyeRotation;
            }
            else if (sprite.SnapCardinals)
            {
                // Snapped sprites are turned back to the nearest quarter turn on screen,
                // and their attach points have to be turned back by the same amount.
                rotation -= (rotation + eyeRotation).Reduced().FlipPositive().RoundToCardinalAngle();
            }
        }

        return rotation.RotateVec(offset);
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

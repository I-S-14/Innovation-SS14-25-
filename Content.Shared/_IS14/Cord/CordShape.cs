// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Shared._IS14.Cord;

/// <summary>
/// The path a cord takes between its two ends. One class per kind of cable, named in
/// YAML with <c>!type:</c> — a coiled cable, a hanging rope and a taut chain are three
/// shapes, not three systems.
/// </summary>
/// <remarks>
/// Shapes are shared by every cord using them, so they must stay stateless: everything
/// that varies arrives in <see cref="CordShapeArgs"/>. This is deliberately pure maths
/// with no rendering types in it, so the same shape could drive a hitbox or a debug
/// readout as easily as the overlay.
/// </remarks>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CordShape
{
    /// <summary>
    /// Appends the points the cord passes through. The start point is already in the
    /// list; the implementation must finish at <see cref="CordShapeArgs.End"/>.
    /// </summary>
    public abstract void GetPoints(in CordShapeArgs args, List<Vector2> points);
}

/// <summary>Everything a shape is given to lay itself out with.</summary>
public readonly struct CordShapeArgs
{
    /// <summary>World position of the anchored end.</summary>
    public readonly Vector2 Start;

    /// <summary>World position of the loose end.</summary>
    public readonly Vector2 End;

    /// <summary>Unit vector from start to end.</summary>
    public readonly Vector2 Direction;

    /// <summary>Unit vector at right angles to <see cref="Direction"/>.</summary>
    public readonly Vector2 Normal;

    /// <summary>Straight-line distance between the ends, in metres.</summary>
    public readonly float Length;

    /// <summary>0 when the cord is pulled taut, 1 when it is fully coiled.</summary>
    public readonly float Slack;

    /// <summary>Seconds, for shapes that move on their own.</summary>
    public readonly float Time;

    public CordShapeArgs(Vector2 start, Vector2 end, float length, float slack, float time)
    {
        Start = start;
        End = end;
        Length = length;
        Slack = slack;
        Time = time;

        Direction = length > 0f ? (end - start) / length : Vector2.UnitX;
        Normal = new Vector2(-Direction.Y, Direction.X);
    }

    /// <summary>Point at <paramref name="t"/> along the straight line, pushed sideways by <paramref name="offset"/>.</summary>
    public Vector2 Along(float t, float offset = 0f)
    {
        return Start + (End - Start) * t + Normal * offset;
    }
}

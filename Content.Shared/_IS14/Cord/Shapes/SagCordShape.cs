// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Shared._IS14.Cord;

/// <summary>
/// A cord hanging under its own weight, bowing out to one side. Right for hoses, ropes
/// and anything limp; the bow flattens as the cord is pulled taut, same as a real one.
/// </summary>
/// <remarks>
/// The droop is a parabola rather than a true catenary. Over the two or three tiles a
/// cord like this ever spans the two are indistinguishable, and a parabola is a
/// multiply instead of a hyperbolic cosine per point.
/// </remarks>
public sealed partial class SagCordShape : CordShape
{
    /// <summary>How far the middle bows out at full slack, in metres.</summary>
    [DataField]
    public float Sag = 0.35f;

    /// <summary>Straight pieces the curve is drawn with. More is smoother and dearer.</summary>
    [DataField]
    public int Segments = 10;

    /// <summary>
    /// Which side of the line the cord bows towards. Positive and negative give the two
    /// sides; use it to keep two cords between the same pair of points apart.
    /// </summary>
    [DataField]
    public float Side = 1f;

    public override void GetPoints(in CordShapeArgs args, List<Vector2> points)
    {
        var segments = Math.Max(2, Segments);
        var sag = Sag * args.Slack * Side;

        for (var i = 1; i < segments; i++)
        {
            var t = i / (float)segments;

            // 4t(1-t) peaks at 1 in the middle and is 0 at both ends, which is exactly
            // where a hanging cord has to be.
            points.Add(args.Along(t, sag * 4f * t * (1f - t)));
        }

        points.Add(args.End);
    }
}

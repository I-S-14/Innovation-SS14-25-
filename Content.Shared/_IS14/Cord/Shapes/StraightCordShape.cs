// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Shared._IS14.Cord;

/// <summary>
/// A straight line. Right for anything under tension — a winch line, a grapple, a
/// tow bar — and the cheapest thing to draw.
/// </summary>
public sealed partial class StraightCordShape : CordShape
{
    public override void GetPoints(in CordShapeArgs args, List<Vector2> points)
    {
        points.Add(args.End);
    }
}

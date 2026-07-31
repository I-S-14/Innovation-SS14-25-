// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Shared._IS14.Cord;

/// <summary>
/// A coiled cable, drawn as a zigzag that flattens out as the cord is paid out. The
/// straightening is the point: it is the only warning a player gets that they are about
/// to reach the end of the cable, and it happens where they are already looking.
/// </summary>
public sealed partial class ZigzagCordShape : CordShape
{
    /// <summary>How far the coil swings to each side at full slack, in metres.</summary>
    [DataField]
    public float Amplitude = 0.24f;

    /// <summary>Length of one zig, in metres. Shorter is a tighter coil.</summary>
    [DataField]
    public float ZigLength = 0.2f;

    /// <summary>
    /// How flat the coil goes when fully taut, as a fraction of <see cref="Amplitude"/>.
    /// Never quite zero, or the cord looks like it snapped before it did.
    /// </summary>
    [DataField]
    public float MinSlack = 0.05f;

    /// <summary>
    /// Fades the swing to nothing at both ends, so the cord meets whatever it is
    /// attached to instead of starting mid-swing beside it.
    /// </summary>
    [DataField]
    public bool Taper = true;

    public override void GetPoints(in CordShapeArgs args, List<Vector2> points)
    {
        // Always an even number of zigs, so the cord leaves and arrives on the centre
        // line rather than ending halfway through a swing.
        var zigs = Math.Max(4, (int)MathF.Round(args.Length / ZigLength / 2f) * 2);
        var amplitude = Amplitude * MathF.Max(MinSlack, args.Slack);

        for (var i = 1; i < zigs; i++)
        {
            var t = i / (float)zigs;
            var taper = Taper ? MathF.Sin(t * MathF.PI) : 1f;
            var side = i % 2 == 0 ? -1f : 1f;

            points.Add(args.Along(t, amplitude * taper * side));
        }

        // Added rather than looped to, so the cord lands exactly on its far end even
        // with the taper switched off.
        points.Add(args.End);
    }
}

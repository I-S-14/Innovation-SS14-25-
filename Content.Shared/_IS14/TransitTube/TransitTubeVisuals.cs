using Robust.Shared.Serialization;

namespace Content.Shared._IS14.TransitTube;

[Serializable, NetSerializable]
public enum TransitTubeStationVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum TransitTubeStationState : byte
{
    Closed,
    Opening,
    Open,
    Closing,
}

[Serializable, NetSerializable]
public enum TransitTubePodVisuals : byte
{
    Occupied,
}

[Serializable, NetSerializable]
public enum TransitTubeVisuals : byte
{
    /// <summary>
    /// Bit field over <see cref="Direction"/>: the ends of this section that actually meet
    /// a neighbour. The joint pieces that dress those ends are drawn from it.
    /// </summary>
    Connections,

    /// <summary>
    /// Whether a junction favours its other branch, which it also shows in its sprite.
    /// </summary>
    Flipped,

    /// <summary>
    /// Whether this section is reckoned 45 degrees round from the way it is turned, which
    /// is what tells a straight run from a diagonal one.
    /// </summary>
    Diagonal,
}

/// <summary>
/// Sprite layers holding the pieces that dress an end where this section meets another.
/// Only the two northward diagonals carry them: the section on the far side of a diagonal
/// joint has the matching southward end, so each corner is dressed exactly once.
/// </summary>
[Serializable, NetSerializable]
public enum TransitTubeJointLayers : byte
{
    Shape,
    North,
    South,
    East,
    West,
    NorthEast,
    NorthWest,
}

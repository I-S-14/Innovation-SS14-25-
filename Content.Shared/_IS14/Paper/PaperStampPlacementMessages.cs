// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Paper;

/// <summary>
///     Sent when the player clicks a spot on an open document while holding a stamp.
/// </summary>
[Serializable, NetSerializable]
public sealed class PaperPlaceStampMessage : BoundUserInterfaceMessage
{
    /// <summary>
    ///     Where on the page the stamp was dropped, normalized to 0..1.
    /// </summary>
    public readonly Vector2 Position;

    /// <summary>
    ///     Angle in radians the player rotated the stamp to before placing it.
    /// </summary>
    public readonly float Rotation;

    public PaperPlaceStampMessage(Vector2 position, float rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}

/// <summary>
///     Sent when the player clicks a spot on an open document to drop their signature there.
/// </summary>
[Serializable, NetSerializable]
public sealed class PaperPlaceSignatureMessage : BoundUserInterfaceMessage
{
    /// <summary>
    ///     Where on the page the signature was dropped, normalized to 0..1.
    /// </summary>
    public readonly Vector2 Position;

    /// <summary>
    ///     Angle in radians the player rotated the signature to before placing it.
    /// </summary>
    public readonly float Rotation;

    public PaperPlaceSignatureMessage(Vector2 position, float rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}

/// <summary>
///     Sent when the player changes their mind about signing and puts the pen away, so the offer
///     stops following them around the document.
/// </summary>
[Serializable, NetSerializable]
public sealed class PaperCancelSignatureMessage : BoundUserInterfaceMessage;

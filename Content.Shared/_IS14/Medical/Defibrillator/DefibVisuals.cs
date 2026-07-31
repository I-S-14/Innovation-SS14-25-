// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.Defibrillator;

/// <summary>What the unit's own sprite is told about itself.</summary>
[Serializable, NetSerializable]
public enum DefibUnitVisuals : byte
{
    /// <summary>Charge bucket, 0..4. Zero means flat, four means full.</summary>
    Charge,

    /// <summary>False draws the "no cell" plate over the front.</summary>
    HasCell,

    /// <summary>Whether the paddles are sitting in their cradle.</summary>
    PaddlesDocked,

    /// <summary>Paddles wielded and the unit charged up.</summary>
    Active,

    /// <summary>Reserved for the sabotage rework — the sprite is already cut for it.</summary>
    Emagged,
}

[Serializable, NetSerializable]
public enum DefibUnitVisualLayers : byte
{
    Base,
    NoCell,
    Charge,
    Paddles,
    Powered,
    Emagged,
}

/// <summary>
/// Layers a wall station or crash cart draws. There is no matching visuals enum: a rack
/// is fed the racked unit's own <see cref="DefibUnitVisuals"/> by the appearance relay,
/// plus <c>ContainedRelayVisuals.HasContents</c>. The rack only has to know how to draw
/// those on its own sheet.
/// </summary>
[Serializable, NetSerializable]
public enum DefibMountVisualLayers : byte
{
    Base,
    Defib,
    Charge,
    Online,
    Emagged,
}

/// <summary>
/// Shared by both sprites: how many charge steps there are, and how a charge fraction
/// maps onto them. The server buckets the number so the client never has to know what
/// counts as "nearly empty".
/// </summary>
public static class DefibChargeLevels
{
    /// <summary>Number of lit steps above empty. The sprites are cut at 25/50/75/100.</summary>
    public const int Max = 4;

    /// <summary>Buckets a 0..1 charge fraction. Anything above empty lights at least one step.</summary>
    public static int FromFraction(float fraction)
    {
        if (fraction <= 0f)
            return 0;

        return Math.Clamp((int)MathF.Ceiling(fraction * Max), 1, Max);
    }
}

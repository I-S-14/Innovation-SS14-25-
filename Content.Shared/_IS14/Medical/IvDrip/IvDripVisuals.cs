// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.IvDrip;

/// <summary>What a drip stand's sprite is told about itself.</summary>
[Serializable, NetSerializable]
public enum IvDripVisuals : byte
{
    /// <summary>An <see cref="IvDripVisualState"/>. The whole pole is redrawn from it.</summary>
    State,

    /// <summary>Whether a bag is hanging, which is what the beaker layer is for.</summary>
    HasPack,

    /// <summary>Bag gauge step, indexed into the sprite's own list of states.</summary>
    FillLevel,

    /// <summary>Colour of what is in the bag.</summary>
    FillColor,
}

/// <summary>
/// The pole's one and only state. Four moods on one layer rather than a stack of
/// toggles, because the sheet draws the whole stand each time — the running frames
/// differ from the idle ones over the length of the thing, not in one corner of it.
/// </summary>
[Serializable, NetSerializable]
public enum IvDripVisualState : byte
{
    /// <summary>Nobody on the end of it.</summary>
    Idle,

    /// <summary>Needle in, set to inject, nothing moving.</summary>
    InjectIdle,

    /// <summary>Needle in, injecting.</summary>
    Injecting,

    /// <summary>Needle in, set to draw, nothing moving.</summary>
    DrawIdle,

    /// <summary>Needle in, drawing.</summary>
    Drawing,
}

[Serializable, NetSerializable]
public enum IvDripVisualLayers : byte
{
    Base,
    Beaker,
    Reagent,
}

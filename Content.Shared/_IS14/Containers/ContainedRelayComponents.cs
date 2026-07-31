// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Containers;

/// <summary>
/// Makes a rack transparent to the hand: clicking it works whatever is racked in it
/// instead. For anything you want presented rather than stored — a wall bracket, a
/// cart, a charging dock — so nobody has to unrack a tool to use it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ContainedInteractionRelayComponent : Component
{
    /// <summary>Container whose contents the click is passed to.</summary>
    [DataField]
    public string SlotId = string.Empty;
}

/// <summary>
/// Puts the racked entity's appearance data onto the rack, so the rack's own sprite can
/// show what it is holding without knowing what that thing is.
/// </summary>
/// <remarks>
/// The rack still needs its own visualizer — this only carries the data across. That
/// split is the point: the meaning of a key belongs to whatever set it, and how a
/// particular frame draws it belongs to that frame.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class ContainedAppearanceRelayComponent : Component
{
    /// <summary>Container whose contents the appearance is read from.</summary>
    [DataField]
    public string SlotId = string.Empty;
}

/// <summary>What a relay tells the rack about itself, on top of whatever it copies over.</summary>
[Serializable, NetSerializable]
public enum ContainedRelayVisuals : byte
{
    /// <summary>Whether the rack has anything in it at all.</summary>
    HasContents,
}

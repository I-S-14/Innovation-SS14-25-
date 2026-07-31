// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Leash;

/// <summary>
/// Something that keeps a tool on a short lead: a defibrillator and its paddles, a fuel
/// pump and its nozzle, a console and its handset. The tool lives in a cradle on this
/// entity, comes out into a hand, and cannot be carried further than the lead allows.
/// </summary>
/// <remarks>
/// The lead itself is only a rule about distance. Whether anyone can see it is up to a
/// <c>CordComponent</c> on the tool, which this has nothing to say about beyond keeping
/// its anchor and slack in step.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeashAnchorComponent : Component
{
    /// <summary>Container the tool sits in while it is stowed.</summary>
    [DataField]
    public string SlotId = "leash_cradle";

    /// <summary>
    /// The tool on the end of the lead, stowed or out. Networked so the client knows the
    /// pair without having to be told about the container.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Leashed;

    /// <summary>
    /// How far the tool can be carried, in metres. Past this the lead runs out and drags
    /// it home — bring the whole anchor along or stay close.
    /// </summary>
    [DataField]
    public float Range = 3f;

    /// <summary>
    /// Whether letting go of the tool sends it home. On for anything on a real lead; off
    /// if you want a tool that may be set down within range and picked up again.
    /// </summary>
    [DataField]
    public bool ReturnOnRelease = true;

    /// <summary>Played at the anchor when the lead runs out and yanks the tool back.</summary>
    [DataField]
    public SoundSpecifier? RecallSound;

    /// <summary>Shown to whoever was holding the tool when the lead ran out.</summary>
    [DataField]
    public LocId RecallPopup = "is14-leash-snapped";

    /// <summary>Shown when the tool is asked for and there is no free hand.</summary>
    [DataField]
    public LocId NoHandPopup = "is14-leash-no-free-hand";

    /// <summary>Shown when the tool is asked for and it is already out.</summary>
    [DataField]
    public LocId AlreadyOutPopup = "is14-leash-already-out";

    /// <summary>Alt-click text for taking the tool off the anchor.</summary>
    [DataField]
    public LocId TakeVerb = "is14-leash-verb-take";

    /// <summary>Alt-click text for putting it back.</summary>
    [DataField]
    public LocId StowVerb = "is14-leash-verb-stow";
}

/// <summary>Marks the far end of a lead, and points back at what it is tied to.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeashedItemComponent : Component
{
    /// <summary>The anchor this belongs to. An item with none is loose and inert.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Anchor;
}

/// <summary>Raised on the anchor once its tool has been taken off it.</summary>
[ByRefEvent]
public readonly record struct LeashTakenEvent(EntityUid Item, EntityUid User);

/// <summary>
/// Raised on the anchor once its tool is back in the cradle, however it got there —
/// stowed by hand, dropped, thrown, or dragged in by the lead.
/// </summary>
[ByRefEvent]
public readonly record struct LeashReturnedEvent(EntityUid Item, bool Recalled);

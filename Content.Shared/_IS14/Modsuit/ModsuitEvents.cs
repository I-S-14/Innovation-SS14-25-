// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modsuit;

/// <summary>
///     DoAfter for sealing or unsealing one part of the suit.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ModsuitSealDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>Part being worked on.</summary>
    public NetEntity Part;

    /// <summary>True when sealing up, false when unsealing.</summary>
    public bool SealingUp;

    public ModsuitSealDoAfterEvent(NetEntity part, bool sealingUp)
    {
        Part = part;
        SealingUp = sealingUp;
    }
}

/// <summary>
///     DoAfter for one round of work on a piece of plating. Raised on the piece itself,
///     which is what the tool was pointed at.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ModsuitRepairDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Raised on a part after it seals or unseals, so visuals and behaviours can react.
/// </summary>
[ByRefEvent]
public readonly record struct ModsuitPartSealedEvent(EntityUid Control, bool Sealed);

/// <summary>
///     Raised on a part after it deploys onto the wearer or folds back into the suit.
/// </summary>
[ByRefEvent]
public readonly record struct ModsuitPartDeployedEvent(EntityUid Control, bool Deployed);

/// <summary>
///     Raised on the control unit when someone puts the suit on or takes it off.
/// </summary>
[ByRefEvent]
public readonly record struct ModsuitWearerChangedEvent(EntityUid? Wearer);

/// <summary>
///     Raised on the suit when the lock or the sabotage state moves.
///
///     One event for all of it rather than one per switch: everything it covers is
///     invisible except through the panel, and the panel redraws itself whole anyway.
///     Without it the readout keeps showing the state of a lock that was opened a minute
///     ago, because nothing else about the suit changed.
/// </summary>
[ByRefEvent]
public readonly record struct ModsuitSecurityChangedEvent;

/// <summary>
///     Raised on the suit to make it let go of whoever is inside — unseal everything and
///     fold it away, whatever the wearer wants.
///
///     An event rather than a method call because the two things that ask for it, the
///     wire panel and the ID lock, both live in systems the suit system already depends
///     on. It is also the single place to hang anything else that should be able to pop
///     a suit open later.
/// </summary>
[ByRefEvent]
public record struct ModsuitForceReleaseEvent(EntityUid? User, bool Handled = false);

/// <summary>
///     DoAfter for one pass of a cutting torch over worn plating.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ModsuitCutDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>Piece of plating under the torch.</summary>
    public NetEntity Part;

    public ModsuitCutDoAfterEvent(NetEntity part)
    {
        Part = part;
    }
}


/// <summary>
///     DoAfter for a screwdriver taken to the panel of a suit somebody is wearing.
/// </summary>
/// <remarks>
///     The wire system has its own panel do-after, but its delay comes straight out of the
///     prototype and there is nowhere to put the penalty a struggling wearer earns. This
///     one exists to own that delay; the toggle itself is still the wire system's.
/// </remarks>
[Serializable, NetSerializable]
public sealed partial class ModsuitPanelDoAfterEvent : SimpleDoAfterEvent;

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

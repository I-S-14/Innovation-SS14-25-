// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.IvDrip;

/// <summary>
/// Sits on whoever has a needle in them. It exists so the patient has something of their
/// own to click: the drip is a piece of furniture across the room, but the needle is in
/// their arm, and getting it out should not require finding the stand first.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IvNeedleComponent : Component
{
    /// <summary>The stand on the other end of the line.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid Drip;

    /// <summary>Alert shown while the needle is in.</summary>
    [DataField]
    public ProtoId<AlertPrototype> Alert = "IS14IvNeedle";
}

/// <summary>Raised on the patient when they click the needle alert.</summary>
public sealed partial class IvNeedleRemoveAlertEvent : BaseAlertEvent;

/// <summary>The doctor getting the needle in.</summary>
[Serializable, NetSerializable]
public sealed partial class IvDripAttachDoAfterEvent : SimpleDoAfterEvent;

/// <summary>The patient getting it back out.</summary>
[Serializable, NetSerializable]
public sealed partial class IvDripDetachDoAfterEvent : SimpleDoAfterEvent;

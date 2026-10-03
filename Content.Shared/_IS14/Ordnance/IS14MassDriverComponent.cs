// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Ordnance;

/// <summary>
/// Mass driver: a floor plate that throws whatever is standing on it in the direction it faces.
/// </summary>
/// <remarks>
/// This is how a charge reaches the test site instead of going off in the lab. Deliberately dumb —
/// it throws, it does not arm anything: the timer lives on the bomb, so the science is in guessing
/// the flight time right.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14MassDriverComponent : Component
{
    /// <summary>How far the throw carries, in tiles.</summary>
    [DataField]
    public float Range = 20f;

    /// <summary>Throw speed. Higher also means a flatter, faster arc.</summary>
    [DataField]
    public float Speed = 14f;

    /// <summary>Beyond this many items on the plate the machine refuses the load, like tg's does.</summary>
    [DataField]
    public int MaxItems = 20;

    /// <summary>Port that fires the driver. Wire the launch console's launch output to it.</summary>
    [DataField]
    public ProtoId<SinkPortPrototype> TriggerPort = "Trigger";

    [DataField]
    public SoundSpecifier? LaunchSound = new SoundPathSpecifier("/Audio/Machines/blastdoor.ogg");

    /// <summary>Quiet period after a shot, so a held button cannot machine-gun the plate.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextReady;
}

[Serializable, NetSerializable]
public enum IS14MassDriverVisuals : byte
{
    Firing,
}

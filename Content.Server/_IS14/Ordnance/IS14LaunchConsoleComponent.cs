// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Ordnance;

/// <summary>
/// Launch control for the ordnance chamber: one countdown, three signals.
/// </summary>
/// <remarks>
/// Everything it controls is wired with the network configurator rather than found by proximity.
/// A console that grabs every door around itself would fling the lab's own airlock open mid-launch;
/// with links, the chamber is exactly the devices somebody chose to put in it, and the same console
/// drives sirens, lights or a second set of shutters just as happily.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14LaunchConsoleComponent : Component
{
    /// <summary>Seconds from pressing launch to the shot itself.</summary>
    [DataField]
    public int Countdown = 10;

    [DataField]
    public int MinCountdown = 3;

    [DataField]
    public int MaxCountdown = 60;

    /// <summary>
    /// How far before the shot the early port fires, and how long after the shot the late one does.
    /// This is the time the shutters get to travel.
    /// </summary>
    [DataField]
    public TimeSpan LeadTime = TimeSpan.FromSeconds(3);

    /// <summary>Fires ahead of the launch — shutters open on this one.</summary>
    [DataField]
    public ProtoId<SourcePortPrototype> PreLaunchPort = "IS14LaunchPre";

    /// <summary>Fires at the moment of the launch — the mass driver sits on this one.</summary>
    [DataField]
    public ProtoId<SourcePortPrototype> LaunchPort = "IS14Launch";

    /// <summary>Fires after the launch — shutters close on this one.</summary>
    [DataField]
    public ProtoId<SourcePortPrototype> PostLaunchPort = "IS14LaunchPost";

    /// <summary>Absolute game time of the shot, or null when idle. Runtime.</summary>
    [ViewVariables]
    public TimeSpan? LaunchAt;

    /// <summary>Which steps of the current sequence have already fired. Runtime.</summary>
    [ViewVariables]
    public bool PreFired;

    [ViewVariables]
    public bool LaunchFired;

    /// <summary>Last countdown value sent to the interface, or -1 while idle. Runtime.</summary>
    [ViewVariables]
    public int LastShownSecond = -1;

    /// <summary>
    /// Local beep when the countdown starts. Warning the station is the chamber's own job: wire a
    /// siren or a light to the early output if the launch needs announcing.
    /// </summary>
    [DataField]
    public SoundSpecifier? AlarmSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");
}

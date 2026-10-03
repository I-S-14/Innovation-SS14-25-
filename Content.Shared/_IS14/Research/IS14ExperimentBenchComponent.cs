// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A machine that measures a sample handed to it and pays the station in research data.
/// One component covers every "insert thing, get points" loop; the currency, the rates
/// and what counts as a sample are all prototype data.
/// </summary>
[RegisterComponent]
public sealed partial class IS14ExperimentBenchComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ResearchPointTypePrototype> PointType;

    /// <summary>What a first-time measurement pays before any multipliers.</summary>
    [DataField]
    public int BaseValue = 20;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>Destroy the sample. False for scanners that hand the thing back.</summary>
    [DataField]
    public bool Consume = true;

    [DataField]
    public IS14ExperimentProfileMode Mode = IS14ExperimentProfileMode.Prototype;

    /// <summary>
    /// Keeps profiles from different kinds of device apart in the archive: scanning a
    /// pistol and dismantling a pistol are different measurements.
    /// </summary>
    [DataField]
    public string ProfileNamespace = "sample";

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1.5);

    /// <summary>Absolute game time the bench is ready again. Runtime.</summary>
    [ViewVariables]
    public TimeSpan NextReady;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Machines/Nuke/confirm_beep.ogg");

    /// <summary>Announce payouts here. Null keeps the result between the machine and its operator.</summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Radio;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A handheld instrument: used on a target instead of being fed a sample. Same archive,
/// same novelty rule, same currencies — the difference is that the subject stays where
/// it is, which is what pathology and live xenofauna need.
/// </summary>
[RegisterComponent]
public sealed partial class IS14ExperimentScannerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ResearchPointTypePrototype> PointType;

    [DataField]
    public int BaseValue = 40;

    /// <summary>What can be scanned at all.</summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>Refuse living subjects. A post-mortem needs a corpse.</summary>
    [DataField]
    public bool RequireDead;

    /// <summary>Refuse corpses. For instruments that measure something alive.</summary>
    [DataField]
    public bool RequireAlive;

    [DataField]
    public IS14ExperimentProfileMode Mode = IS14ExperimentProfileMode.Mob;

    [DataField]
    public string ProfileNamespace = "scan";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/beep.ogg");

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;
}

[Serializable, NetSerializable]
public sealed partial class IS14ExperimentScanDoAfterEvent : SimpleDoAfterEvent;

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// The First Department's bench: takes a confiscated piece of enemy hardware apart and
/// uncovers a line of research that did not exist on the map a minute ago.
/// </summary>
/// <remarks>
/// This is what makes security and science talk to each other. Contraband is the only thing
/// that reveals a hidden topic, security is the only department that legally has contraband,
/// and the topics it reveals are the ones security wants: better armour, detectors, special
/// means. Handing the confiscated e-sword over is therefore worth more than burying it.
///
/// Working without a sanction is left possible and merely criminal: a traitor scientist in a
/// maintenance lab is the point, not an oversight.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14ReverseEngineeringBenchComponent : Component
{
    /// <summary>What the bench will even look at. Usually the contraband tag.</summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// Stamps a sanction form must carry for the work to be legal. The data is the same either
    /// way — the difference is the log entry and, with luck, the trial.
    /// </summary>
    [DataField]
    public List<string> SanctionStamps = new()
    {
        "stamp-component-stamped-name-rd",
    };

    /// <summary>Share of the payout for work done without a sanction.</summary>
    [DataField]
    public float UnsanctionedMultiplier = 0.6f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(6);

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/Machines/circuitprinter.ogg");

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;

    /// <summary>Technology being uncovered by the cycle in progress. Runtime.</summary>
    [ViewVariables]
    public string? PendingTechnology;

    [ViewVariables]
    public bool PendingSanctioned;

    [ViewVariables]
    public EntityUid? User;

    [ViewVariables]
    public TimeSpan? FinishesAt;
}

/// <summary>
/// A sanction for work on captured hardware: the paper that makes reverse engineering legal.
/// Carried by the scientist, stamped by the people who answer for it.
/// </summary>
[RegisterComponent]
public sealed partial class IS14SanctionFormComponent : Component;

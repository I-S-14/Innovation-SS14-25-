// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// An instrument that can go wrong. Failure is not "nothing happened": the sample is lost, the
/// room gets a reminder, and what comes out is a negative result.
/// </summary>
/// <remarks>
/// Three things make risk worth having. Protective gear finally means something, because wearing
/// it is what cuts the odds. A failure still pays a little, so a bad roll is a setback and not a
/// wasted trip. And a failed experiment is safer the next time it is run, which is exactly how a
/// negative result works in practice — you now know one way it breaks.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14ExperimentRiskComponent : Component
{
    /// <summary>Chance a measurement goes wrong, 0..1, before gear and experience.</summary>
    [DataField]
    public float Risk = 0.15f;

    /// <summary>
    /// Equipped items that count as procedure: a biohazard suit, eye protection, a sealed
    /// suit. Named by the components that gear already carries, so no clothing needed editing.
    /// </summary>
    [DataField]
    public EntityWhitelist? Safety;

    /// <summary>How much of the risk one piece of protective gear removes.</summary>
    [DataField]
    public float SafetyReduction = 0.35f;

    /// <summary>Share of the payout a failed experiment still earns.</summary>
    [DataField]
    public float NegativeShare = 0.15f;

    /// <summary>How much safer a repeat of a failed measurement is, per earlier failure.</summary>
    [DataField]
    public float LearningReduction = 0.2f;

    /// <summary>Spawned where it went wrong.</summary>
    [DataField]
    public EntProtoId? Effect = "Spark";

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
}

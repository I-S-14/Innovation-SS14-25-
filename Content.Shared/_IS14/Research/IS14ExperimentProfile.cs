// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Research;

/// <summary>
/// How a device turns a sample into a profile key. The key is what the archive
/// remembers, so it decides what counts as "the same measurement again".
/// </summary>
[Serializable, NetSerializable]
public enum IS14ExperimentProfileMode : byte
{
    /// <summary>The sample's prototype. Two steel sheets are the same measurement.</summary>
    Prototype,

    /// <summary>Species plus the damage that dominates the body — a cause of death.</summary>
    Mob,

    /// <summary>What the sample is physically made of.</summary>
    Material,

    /// <summary>The sample describes itself and nothing else does.</summary>
    Custom,
}

/// <summary>
/// Raised on the sample (or the scanned target) to let it describe itself before the
/// device falls back to <see cref="IS14ExperimentProfileMode"/>.
/// </summary>
/// <remarks>
/// This is the extension point for the whole framework: anything that wants to be
/// worth research data — a survey form, a captured weapon, a tagged expedition find —
/// answers this event instead of needing its own machine.
/// </remarks>
public sealed class IS14GetExperimentProfileEvent : EntityEventArgs
{
    /// <summary>The bench or scanner asking.</summary>
    public readonly EntityUid Device;

    /// <summary>Profile key. Left null, the device builds one from its own mode.</summary>
    public string? ProfileKey;

    /// <summary>Scales the device's base value. Job weight, sample quality, and so on.</summary>
    public float Multiplier = 1f;

    /// <summary>Refuse the measurement outright, with a reason for the popup.</summary>
    public bool Refuse;

    public string? RefuseReason;

    /// <summary>Keep the sample even if the device would normally consume it.</summary>
    public bool PreventConsume;

    /// <summary>Pay in a different currency than the device's default.</summary>
    public ProtoId<ResearchPointTypePrototype>? PointTypeOverride;

    public IS14GetExperimentProfileEvent(EntityUid device)
    {
        Device = device;
    }
}

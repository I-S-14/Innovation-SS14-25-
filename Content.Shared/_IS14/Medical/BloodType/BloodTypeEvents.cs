// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// Asked of an entity when something wants to know its blood group.
/// </summary>
/// <remarks>
/// Raised before the DNA roll, so anything that wants the last word on somebody's blood —
/// a changeling wearing a stolen body, a synthetic with a printed circulatory system, a
/// gamemode that hands out rare groups — answers here instead of racing the component.
/// </remarks>
[ByRefEvent]
public record struct GetBloodTypeEvent(EntityUid Entity)
{
    /// <summary>Set this to answer. Last writer wins, so subscribe with priority if it matters.</summary>
    public ProtoId<BloodTypePrototype>? Type;
}

/// <summary>
/// The verdict on putting one group into another, and why.
/// </summary>
public sealed class BloodCompatibility
{
    /// <summary>Whether the blood is accepted. Everything else is the reasoning behind it.</summary>
    public bool Compatible = true;

    /// <summary>
    /// Antigens the recipient tolerates this once and will not tolerate again. Non-empty
    /// only on an otherwise compatible verdict.
    /// </summary>
    public readonly HashSet<ProtoId<BloodAntigenPrototype>> Sensitizing = new();

    /// <summary>Antigens the recipient has antibodies for right now.</summary>
    public readonly HashSet<ProtoId<BloodAntigenPrototype>> Rejected = new();

    /// <summary>True when the two bloods are not even the same substance.</summary>
    public bool WrongSpecies;
}

/// <summary>
/// Raised on the recipient once a verdict has been reached, before it is acted on.
/// </summary>
/// <remarks>
/// The place to bend the rules: a trait that makes somebody a universal recipient, an
/// implant that suppresses rejection, a curse that rejects everything. Mutate
/// <see cref="Compatibility"/> and the transfusion follows what you leave behind.
/// </remarks>
[ByRefEvent]
public record struct GetBloodCompatibilityEvent(
    EntityUid Recipient,
    ProtoId<BloodTypePrototype>? DonorType,
    BloodCompatibility Compatibility);

/// <summary>
/// Raised on the recipient before blood enters them. Cancel to refuse it outright.
/// </summary>
[ByRefEvent]
public record struct BloodTransfusionAttemptEvent(
    EntityUid Recipient,
    Solution Donated,
    EntityUid? Source)
{
    public bool Cancelled;
}

/// <summary>
/// Raised on the recipient after a transfusion has been resolved.
/// </summary>
/// <remarks>
/// <paramref name="Accepted"/> is what went in as blood and <paramref name="Rejected"/> is
/// what was turned into hemolysate on the way, so a subscriber can tell a successful unit
/// from a mistake without repeating the compatibility check.
/// </remarks>
[ByRefEvent]
public readonly record struct BloodTransfusedEvent(
    EntityUid Recipient,
    Solution Accepted,
    Solution Rejected,
    EntityUid? Source);

/// <summary>
/// Raised on a mob the first time an antigen teaches its immune system to hate that antigen.
/// </summary>
[ByRefEvent]
public readonly record struct BloodSensitizedEvent(
    EntityUid Recipient,
    ProtoId<BloodAntigenPrototype> Antigen);

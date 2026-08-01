// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// What a reading of some blood found: what kind it is, what is on it, and what that is
/// called if it is called anything.
/// </summary>
/// <remarks>
/// <see cref="Antigens"/> and not <see cref="Type"/> is the truth here. A bag two donors
/// went into has a definite set of antigens but need not have a name — nothing says the
/// union of two groups is itself a group once somebody adds a fourth antigen. Everything
/// that decides anything reads the set; the name is for showing to players, and is allowed
/// to be null without that meaning "clean".
/// </remarks>
public readonly record struct BloodSample(
    ProtoId<ReagentPrototype> Reagent,
    IReadOnlySet<ProtoId<BloodAntigenPrototype>> Antigens,
    ProtoId<BloodTypePrototype>? Type);

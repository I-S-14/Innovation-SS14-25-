// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Medical.Circulation;

/// <summary>
/// Raised on a body to ask how fast its heart is allowed to go.
/// </summary>
/// <remarks>
/// The ceiling is where heart disease bites: an unhealthy heart cannot race, so it cannot
/// compensate, so a wound that a healthy person would shrug off puts it into shock. Asked
/// rather than stored so that the circulation system never has to know what a disease is.
/// </remarks>
[ByRefEvent]
public record struct GetHeartCeilingEvent(float Ceiling);

/// <summary>
/// Raised on a body to ask how much oxygen it currently wants.
/// </summary>
/// <remarks>
/// One is a person standing still. Running, fighting and fever push it up; cold, sedation
/// and lying down pull it down — which is why telling a bleeding patient to stay still is
/// real advice here rather than flavour, and why cryogenics work at all.
/// </remarks>
[ByRefEvent]
public record struct GetOxygenDemandEvent(float Demand);

/// <summary>Raised on a body when it moves between shock stages, in either direction.</summary>
[ByRefEvent]
public readonly record struct ShockStageChangedEvent(ShockStage Old, ShockStage New);

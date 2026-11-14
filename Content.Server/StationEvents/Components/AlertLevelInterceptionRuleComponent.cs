// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.StationEvents.Events;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// Added to gamerule entity prototypes to make them change the station's alert level when starting the gamerule.
/// This will only apply if the station is currently on its default alert level.
/// </summary>
[RegisterComponent, Access(typeof(AlertLevelInterceptionRule))]
public sealed partial class AlertLevelInterceptionRuleComponent : Component
{
    /// <summary>
    /// Alert level to set the station to when the event starts.
    /// </summary>
    [DataField]
    public string AlertLevel = "blue";

    /// <summary>
    /// Goobstation.
    /// Whether or not to override the current alert level, if it isn't green.
    /// </summary>
    [DataField]
    public bool OverrideAlert = false;

    /// <summary>
    /// Goobstation.
    /// Whether the alert level should be changeable.
    /// </summary>
    [DataField]
    public bool Locked = false;
}

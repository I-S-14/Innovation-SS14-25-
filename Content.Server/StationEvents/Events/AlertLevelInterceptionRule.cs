// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.StationEvents.Components;
using Content.Shared.AlertLevel;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Handler for events setting the station alert level.
/// </summary>
/// <seealso cref="AlertLevelInterceptionRuleComponent"/>
public sealed partial class AlertLevelInterceptionRule : StationEventSystem<AlertLevelInterceptionRuleComponent>
{
    [Dependency] private AlertLevelSystem _alertLevel = default!;

    protected override void Started(EntityUid uid, AlertLevelInterceptionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args) // Goobstation - Changed an indent.
    {
        base.Started(ent, ref args);

        if (!TryGetRandomStation(out var chosenStation))
            return;
        if (_alertLevelSystem.GetLevel(chosenStation.Value) != "green" && component.OverrideAlert == false) // Goobstation
            return;

        _alertLevelSystem.SetLevel(chosenStation.Value, component.AlertLevel, true, true, true, component.Locked); // Goobstation
    }
}
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.StationEvents.Components;
using Content.Shared.AlertLevel;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Content.Server.AlertLevel;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Handler for events setting the station alert level.
/// </summary>
/// <seealso cref="AlertLevelInterceptionRuleComponent"/>
public sealed partial class AlertLevelInterceptionRule : StationEventSystem<AlertLevelInterceptionRuleComponent>
{
    //IS14-change start: restore declarations used by merged implementation.
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    //IS14-change end

    //IS14-change start: retain fork fields lost during merge.
    //IS14-change end



    protected override void Started(EntityUid uid, AlertLevelInterceptionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args) // Goobstation - Changed an indent.
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;
        if (TryComp<AlertLevelComponent>(chosenStation, out var alert)
            && alert.CurrentAlertLevel != alert.DefaultAlertLevel && !component.OverrideAlert)
            return;

        _alertLevelSystem.SetLevel(chosenStation.Value, component.AlertLevel, true, true, true, component.Locked); // Goobstation
    }
}
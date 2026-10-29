// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs; // Goobstation
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers; // Goobstation
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Database; // Goobstation
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Handler for events that spawn gas at vents on the station.
/// </summary>
/// <seealso cref="GasLeakRuleComponent"/>
public sealed partial class GasLeakRule : StationEventSystem<GasLeakRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    protected override void Started(Entity<GasLeakRuleComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

        var gasLeak = ent.Comp1;
        var gameRule = ent.Comp2;

        if (!TryComp<StationEventComponent>(ent, out var stationEvent))
            return;

        var stationVents = Station.GetEntitiesWithComponentOnStation<GasVentScrubberComponent>(true);
        if (stationVents.Count == 0)
        {
            ForceEndSelf((ent, gameRule));
            return;
        }

        var targetVent = RobustRandom.Pick(stationVents);

        gasLeak.FoundTile = true;
        gasLeak.TargetCoords = Transform(targetVent).Coordinates;
        gasLeak.TargetGrid = Transform(targetVent).GridUid!.Value;

            component.LeakGas = RobustRandom.Pick(component.LeakableGases);
            // Was 50-50 on using normal distribution.
            var totalGas = RobustRandom.Next(component.MinimumGas, component.MaximumGas);
            component.MolesPerSecond = RobustRandom.Next(component.MinimumMolesPerSecond, component.MaximumMolesPerSecond);
            // Goobstation start
            if (gameRule.Delay is { } startAfter)
            {
                stationEvent.EndTime = _timing.CurTime +
                                       TimeSpan.FromSeconds(totalGas / component.MolesPerSecond +
                                                            startAfter.Next(RobustRandom));
                _adminLogger.Add(LogType.EventRan, LogImpact.High, $"Gasleak placing {totalGas} moles of {component.LeakGas} at {component.TargetTile} in grid {component.TargetGrid}.");
                _chat.SendAdminAnnouncement($"Gasleak placing {totalGas} moles of {component.LeakGas} at {component.TargetTile} in grid {component.TargetGrid}.");
            }
            // Goobstation end
        }

        // Look technically if you wanted to guarantee a leak you'd do this in announcement but having the announcement
        // there just to fuck with people even if there is no valid tile is funny.
    }

    protected override void ActiveTick(EntityUid uid, GasLeakRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        component.TimeUntilLeak -= frameTime;

        if (component.TimeUntilLeak > 0f)
            return;
        component.TimeUntilLeak += component.LeakCooldown;

        if (!component.FoundTile ||
            component.TargetGrid == default ||
            Deleted(component.TargetGrid) ||
            !_atmosphere.IsSimulatedGrid(component.TargetGrid))
        {
            ForceEndSelf((uid, gameRule));
            return;
        }

        var environment = _atmosphere.GetTileMixture(component.TargetGrid, null, (Vector2i)component.TargetCoords.Position, true);

        environment?.AdjustMoles(component.LeakGas, component.LeakCooldown * component.MolesPerSecond);
    }

    protected override void Ended(Entity<GasLeakRuleComponent> rule, ref GameRuleEndedEvent args)
    {
        base.Ended(rule, ref args);
        Spark(rule);
    }

    private void Spark(Entity<GasLeakRuleComponent> rule)
    {
        if (!(RobustRandom.NextFloat() <= rule.Comp.SparkChance))
            return;

        if (!rule.Comp.FoundTile ||
            rule.Comp.TargetGrid == default ||
            (!Exists(rule.Comp.TargetGrid) ? EntityLifeStage.Deleted : MetaData(rule.Comp.TargetGrid).EntityLifeStage) >= EntityLifeStage.Deleted ||
            !_atmosphere.IsSimulatedGrid(rule.Comp.TargetGrid))
        {
            return;
        }

        // Don't want it to be so obnoxious as to instantly murder anyone in the area but enough that
        // it COULD start potentially start a bigger fire.
        _atmosphere.HotspotExpose(rule.Comp.TargetGrid, (Vector2i)rule.Comp.TargetCoords.Position, 700f, 50f, null, true);
        Audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sparks4.ogg"), rule.Comp.TargetCoords);
    }
}

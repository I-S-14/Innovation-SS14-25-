// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared.APC;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// Share of the station's APCs that are switched on and not starved of power.
/// Averaged, so a blackout costs the fund even once it has been fixed.
/// </summary>
public sealed partial class PoweredApcsMetric : SampledPlanMetric
{
    protected override float Read(in PlanMetricArgs args)
    {
        var stations = args.System<StationSystem>();
        var total = 0;
        var powered = 0;

        var query = args.EntityManager.EntityQueryEnumerator<ApcComponent>();
        while (query.MoveNext(out var uid, out var apc))
        {
            if (stations.GetOwningStation(uid) != args.Station)
                continue;

            total++;
            if (apc.MainBreakerEnabled && apc.LastChargeState != ApcChargeState.Lack)
                powered++;
        }

        // A station with no APCs at all is not a failing station.
        return total == 0 ? 1f : (float)powered / total;
    }
}

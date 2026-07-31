// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Monitor.Components;
using Content.Server.Station.Systems;
using Content.Shared.Atmos.Monitor;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// Share of the station's air alarms reading normal. Averaged over the period, so a
/// hull breach costs Engineering money for as long as the alarm stays lit — patching
/// it late is cheaper than not patching it, and never as good as patching it fast.
/// </summary>
public sealed partial class AirAlarmsNormalMetric : SampledPlanMetric
{
    protected override float Read(in PlanMetricArgs args)
    {
        var stations = args.System<StationSystem>();
        var total = 0;
        var normal = 0;

        var query = args.EntityManager.EntityQueryEnumerator<AirAlarmComponent>();
        while (query.MoveNext(out var uid, out var alarm))
        {
            if (stations.GetOwningStation(uid) != args.Station)
                continue;

            total++;
            if (alarm.State == AtmosAlarmType.Normal)
                normal++;
        }

        // A station with no air alarms at all is not a failing station.
        return total == 0 ? 1f : (float)normal / total;
    }
}

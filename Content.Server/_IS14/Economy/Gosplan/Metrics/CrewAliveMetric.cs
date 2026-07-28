// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Economy;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// Share of the crew on the station's payroll that is still alive. Averaged over the
/// period — an hour in a body bag isn't undone by a revival at the last minute.
/// </summary>
public sealed partial class CrewAliveMetric : SampledPlanMetric
{
    protected override float Read(in PlanMetricArgs args)
    {
        var total = 0;
        var alive = 0;

        var query = args.EntityManager.EntityQueryEnumerator<JobSalaryComponent, MobStateComponent>();
        while (query.MoveNext(out _, out var salary, out var mobState))
        {
            if (salary.Station != args.Station)
                continue;

            total++;
            if (mobState.CurrentState == MobState.Alive)
                alive++;
        }

        // An empty station is not a failing station.
        return total == 0 ? 1f : (float)alive / total;
    }
}

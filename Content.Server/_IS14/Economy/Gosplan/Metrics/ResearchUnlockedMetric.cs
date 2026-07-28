// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Station.Systems;
using Content.Shared.Research.Components;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>Technologies the station's research servers unlocked during the period.</summary>
public sealed partial class ResearchUnlockedMetric : DeltaPlanMetric
{
    protected override int Read(in PlanMetricArgs args)
    {
        var stations = args.System<StationSystem>();
        var count = 0;

        var query = args.EntityManager.EntityQueryEnumerator<ResearchServerComponent, TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out _, out var database))
        {
            if (stations.GetOwningStation(uid) != args.Station)
                continue;

            count += database.UnlockedTechnologies.Count;
        }

        return count;
    }
}

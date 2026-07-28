// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.AlertLevel;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// Share of the period the station spent on its default alert level. Every minute of
/// a raised code costs the department money, including the ones nobody lifted in time.
/// </summary>
public sealed partial class PeaceTimeMetric : SampledPlanMetric
{
    protected override float Read(in PlanMetricArgs args)
    {
        if (!args.EntityManager.TryGetComponent<AlertLevelComponent>(args.Station, out var alert)
            || alert.AlertLevels == null)
        {
            return 1f;
        }

        return alert.CurrentLevel == alert.AlertLevels.DefaultLevel ? 1f : 0f;
    }
}

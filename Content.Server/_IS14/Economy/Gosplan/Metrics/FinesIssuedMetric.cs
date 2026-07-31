// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._IS14.Economy.Fines;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// Fines Security wrote during the period. Voided ones are not counted, and because the
/// delta is taken against the running total, voiding a fine after the fact takes the
/// credit for it back — paperwork written to pad the plan has to survive review.
/// </summary>
public sealed partial class FinesIssuedMetric : DeltaPlanMetric
{
    protected override int Read(in PlanMetricArgs args)
    {
        if (!args.EntityManager.TryGetComponent<StationFinesComponent>(args.Station, out var fines))
            return 0;

        var count = 0;
        foreach (var fine in fines.Fines)
        {
            if (!fine.Voided)
                count++;
        }

        return count;
    }
}

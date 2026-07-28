// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// A metric nothing can read off the station on demand: it is fed by events as they
/// happen and reset when the period turns over.
/// </summary>
public abstract partial class AccumulatorPlanMetric : PlanMetric
{
    public sealed override PlanMetricState CreateState() => new AccumulatorState();

    public sealed override void PeriodStarted(in PlanMetricArgs args)
    {
        ((AccumulatorState)args.State).Total = 0;
    }

    public sealed override float GetValue(in PlanMetricArgs args)
    {
        return ((AccumulatorState)args.State).Total;
    }

    /// <summary>Counts something towards this period's total.</summary>
    protected static void Add(in PlanMetricArgs args, int amount)
    {
        ((AccumulatorState)args.State).Total += amount;
    }

    private sealed class AccumulatorState : PlanMetricState
    {
        public int Total;
    }
}

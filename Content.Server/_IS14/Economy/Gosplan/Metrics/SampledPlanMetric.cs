// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// A metric that describes a state rather than an event. It is read on every sampling
/// tick and scored as the average over the whole period, so a department cannot fix
/// everything one minute before the report and call it a fulfilled plan.
/// </summary>
public abstract partial class SampledPlanMetric : PlanMetric
{
    public sealed override PlanMetricState CreateState() => new SampledState();

    public sealed override void PeriodStarted(in PlanMetricArgs args)
    {
        var state = (SampledState)args.State;
        state.Sum = 0f;
        state.Count = 0;
    }

    public sealed override void Sample(in PlanMetricArgs args)
    {
        var state = (SampledState)args.State;
        state.Sum += Read(args);
        state.Count++;
    }

    public sealed override float GetValue(in PlanMetricArgs args)
    {
        var state = (SampledState)args.State;

        // Fall back to a live reading until the first sample of the period lands.
        return state.Count == 0 ? Read(args) : state.Sum / state.Count;
    }

    /// <summary>Takes one reading of the station, normally a 0..1 ratio.</summary>
    protected abstract float Read(in PlanMetricArgs args);

    private sealed class SampledState : PlanMetricState
    {
        public float Sum;
        public int Count;
    }
}

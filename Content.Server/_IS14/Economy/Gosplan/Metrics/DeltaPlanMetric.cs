// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// A metric measured as how far a running total moved during the period. Only the
/// work done this period counts — last shift's technologies don't pay twice.
/// </summary>
public abstract partial class DeltaPlanMetric : PlanMetric
{
    public sealed override PlanMetricState CreateState() => new DeltaState();

    public sealed override void PeriodStarted(in PlanMetricArgs args)
    {
        ((DeltaState)args.State).Start = Read(args);
    }

    public sealed override float GetValue(in PlanMetricArgs args)
    {
        return Math.Max(0, Read(args) - ((DeltaState)args.State).Start);
    }

    /// <summary>Reads the running total the delta is taken from.</summary>
    protected abstract int Read(in PlanMetricArgs args);

    private sealed class DeltaState : PlanMetricState
    {
        public int Start;
    }
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Economy.EconomyMonitor;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// Something the Plan can measure. One class per measurement, named in YAML with
/// <c>!type:</c> — adding a quota for a new department means dropping a file next to
/// these, and neither <see cref="GosplanSystem"/> nor the board has to learn about it.
/// </summary>
/// <remarks>
/// A metric object lives on the prototype and is shared by every station, so it must
/// stay stateless. Anything that changes during a period belongs in the
/// <see cref="PlanMetricState"/> the metric hands out in <see cref="CreateState"/>.
/// </remarks>
[ImplicitDataDefinitionForInheritors]
public abstract partial class PlanMetric
{
    /// <summary>Creates the per-station scratch space this metric reads and writes.</summary>
    public abstract PlanMetricState CreateState();

    /// <summary>Called when a period begins, and once when the plan is first issued.</summary>
    public virtual void PeriodStarted(in PlanMetricArgs args)
    {
    }

    /// <summary>Called on the sampling tick, for metrics that watch a running state.</summary>
    public virtual void Sample(in PlanMetricArgs args)
    {
    }

    /// <summary>Called for every credit movement anywhere in the station's economy.</summary>
    public virtual void Transaction(in PlanMetricArgs args, EconomyTransactionEvent ev)
    {
    }

    /// <summary>Where the department stands right now, in the quota's own unit.</summary>
    public abstract float GetValue(in PlanMetricArgs args);
}

/// <summary>
/// Per-station progress of one metric. Subclassed by the metric shape that owns it —
/// nothing outside that shape ever looks inside.
/// </summary>
public abstract class PlanMetricState
{
}

/// <summary>Everything a metric is given to work with.</summary>
public readonly struct PlanMetricArgs
{
    public readonly IEntityManager EntityManager;

    /// <summary>Station being scored.</summary>
    public readonly EntityUid Station;

    /// <summary>Quota this metric was configured on, for its fund and target.</summary>
    public readonly PlanQuotaPrototype Quota;

    /// <summary>This metric's own state for this station.</summary>
    public readonly PlanMetricState State;

    public PlanMetricArgs(
        IEntityManager entityManager,
        EntityUid station,
        PlanQuotaPrototype quota,
        PlanMetricState state)
    {
        EntityManager = entityManager;
        Station = station;
        Quota = quota;
        State = state;
    }

    public T System<T>() where T : IEntitySystem => EntityManager.System<T>();
}

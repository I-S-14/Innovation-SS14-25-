// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._IS14.Economy.Gosplan.Metrics;
using Content.Shared._IS14.Economy.Gosplan;

namespace Content.Server._IS14.Economy.Gosplan;

/// <summary>
/// Tracks progress against the station's plan. Added automatically to any station
/// that has economy accounts, once the accounts themselves exist.
/// </summary>
[RegisterComponent]
public sealed partial class StationPlanComponent : Component
{
    /// <summary>1-based number of the period currently being worked.</summary>
    public int PeriodIndex = 1;

    /// <summary>Absolute game time the current period is scored at.</summary>
    public TimeSpan NextEvaluation;

    /// <summary>Quota prototype ID to its live progress.</summary>
    public Dictionary<string, PlanQuotaState> Quotas = new();

    /// <summary>
    /// Station account prototype ID to how that department is doing overall. A department
    /// can carry any number of quotas, but it is sanctioned and awarded as one.
    /// </summary>
    public Dictionary<string, PlanFundState> Funds = new();

    /// <summary>Scored periods, newest last.</summary>
    public List<PlanPeriodEntry> History = new();

    /// <summary>Station account prototype ID currently holding the red banner.</summary>
    public string BannerFund = string.Empty;

    /// <summary>Period the banner was won for. Zero while nobody holds it.</summary>
    public int BannerPeriod;

    /// <summary>Game time the last sample of the continuous metrics was taken.</summary>
    public TimeSpan NextSample;
}

/// <summary>Progress of a single quota within the current period.</summary>
public sealed class PlanQuotaState
{
    /// <summary>
    /// Whatever the quota's metric needs to remember. Created by the metric itself, so
    /// the plan never has to know what shape a measurement takes.
    /// </summary>
    public readonly PlanMetricState Metric;

    /// <summary>Fulfillment scored at the end of the previous period.</summary>
    public float? LastFulfillment;

    public PlanQuotaState(PlanMetricState metric)
    {
        Metric = metric;
    }
}

/// <summary>
/// How a whole department is doing. Kept apart from the individual quotas because a
/// department failing one metric out of three has not failed its plan.
/// </summary>
public sealed class PlanFundState
{
    /// <summary>Mean fulfillment scored at the end of the previous period.</summary>
    public float? LastFulfillment;

    /// <summary>How many periods in a row this department has been below the threshold.</summary>
    public int FailStreak;
}

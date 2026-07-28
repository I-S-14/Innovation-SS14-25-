// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._IS14.Economy.Gosplan.Metrics;
using Content.Server._IS14.Economy.Gosplan.Units;
using Content.Shared._IS14.Economy;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.Gosplan;

/// <summary>
/// One line of the station's plan: a department, something measurable it is expected
/// to deliver over a plan period, and what Gosplan pays for delivering it.
/// </summary>
/// <remarks>
/// Server-only, because the measuring is: the board sees quotas as finished text and
/// numbers in its UI state. The client registers this kind as ignored.
/// </remarks>
[Prototype("planQuota")]
public sealed partial class PlanQuotaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Short name of the quota shown on the soc-competition board.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>One line explaining what the department has to do.</summary>
    [DataField]
    public LocId? Description;

    /// <summary>
    /// Station account this quota belongs to. Funding is paid here, and a revenue
    /// metric measures this account unless it is pointed somewhere else.
    /// A quota whose fund the station doesn't have is skipped entirely.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StationAccountPrototype> Fund;

    /// <summary>What this quota measures. Written as <c>!type:SomeMetric</c>.</summary>
    [DataField(required: true)]
    public PlanMetric Metric = default!;

    /// <summary>
    /// Value counting as 100% fulfillment, in whatever the metric returns. Ratio
    /// metrics use 0..1, counting metrics use an absolute number per period.
    /// </summary>
    [DataField(required: true)]
    public float Target;

    /// <summary>
    /// Credits paid into the fund at exactly 100% fulfillment. Below that the payout
    /// shrinks proportionally, above it grows up to the overfulfillment cap.
    /// </summary>
    [DataField]
    public int Payout = 10000;

    /// <summary>How the numbers are written on the board. <c>!type:PercentQuotaUnit</c> and friends.</summary>
    [DataField]
    public PlanQuotaUnit Unit = new CountQuotaUnit();

    /// <summary>Display order on the board. Lower comes first.</summary>
    [DataField]
    public int Order;
}

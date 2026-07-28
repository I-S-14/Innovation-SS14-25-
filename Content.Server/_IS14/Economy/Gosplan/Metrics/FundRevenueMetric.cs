// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._IS14.Economy.Components;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.Gosplan.Metrics;

/// <summary>
/// Credits paid into a department fund during the period. Only money coming in counts,
/// and only into the one account — a department cannot meet its plan by shuffling its
/// own budget around.
/// </summary>
public sealed partial class FundRevenueMetric : AccumulatorPlanMetric
{
    /// <summary>
    /// Account being watched. Defaults to the fund the quota itself belongs to, which is
    /// what a department revenue target wants; set it to measure somebody else's takings.
    /// </summary>
    [DataField]
    public ProtoId<StationAccountPrototype>? Fund;

    public override void Transaction(in PlanMetricArgs args, EconomyTransactionEvent ev)
    {
        // Sanctions and spending are somebody else's problem; this is a revenue target.
        if (ev.Delta <= 0)
            return;

        if (!args.EntityManager.TryGetComponent<StationBankAccountsComponent>(args.Station, out var accounts))
            return;

        var fund = Fund ?? args.Quota.Fund;

        if (accounts.AccountNumbers.TryGetValue(fund, out var accountNumber) && accountNumber == ev.AccountNumber)
            Add(args, ev.Delta);
    }
}

using System.Text;
using Content.Server._IS14.Economy.Components;
using Content.Server._IS14.Economy.Gosplan.Metrics;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.Gosplan;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.Gosplan;

/// <summary>
/// Scores the station's plan every period and funds departments by how well they did.
///
/// Money enters the round here and nowhere else: department budgets are a one-time
/// grant, so without the plan a station simply runs dry halfway through the shift.
/// Doing your job well is what keeps the lights on for everyone downstream.
///
/// What each quota measures lives in its <see cref="PlanMetric"/>, not here — this
/// system only knows how to sample, score and pay.
/// </summary>
public sealed class GosplanSystem : EntitySystem
{
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>How often the sampled metrics are read.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();

        // Accounts have to exist before quotas can be bound to them.
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized, after: new[] { typeof(StationBankAccountSystem) });
        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);
    }

    private void OnStationInitialized(StationInitializedEvent ev)
    {
        if (!_cfg.GetCVar(IS14CVars.GosplanEnabled))
            return;

        if (!TryComp<StationBankAccountsComponent>(ev.Station, out var accounts))
            return;

        var plan = EnsureComp<StationPlanComponent>(ev.Station);
        var period = TimeSpan.FromSeconds(_cfg.GetCVar(IS14CVars.GosplanPeriodSeconds));

        plan.NextEvaluation = _timing.CurTime + period;
        plan.NextSample = _timing.CurTime + SampleInterval;

        foreach (var proto in _prototypes.EnumeratePrototypes<PlanQuotaPrototype>())
        {
            // A quota is only meaningful if the station actually funds that department.
            if (!accounts.AccountNumbers.ContainsKey(proto.Fund))
                continue;

            var state = new PlanQuotaState(proto.Metric.CreateState());
            plan.Quotas[proto.ID] = state;

            proto.Metric.PeriodStarted(Args(ev.Station, proto, state));
        }

        Log.Info($"Gosplan: station {ToPrettyString(ev.Station)} received a plan with {plan.Quotas.Count} quotas, period {period.TotalMinutes:0} min.");

        AnnounceNewPeriod(ev.Station, plan);
    }

    /// <summary>Offers every credit movement to the metrics that care about one.</summary>
    private void OnTransaction(EconomyTransactionEvent ev)
    {
        var query = EntityQueryEnumerator<StationPlanComponent>();
        while (query.MoveNext(out var station, out var plan))
        {
            foreach (var (proto, state) in Quotas(plan))
            {
                proto.Metric.Transaction(Args(station, proto, state), ev);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StationPlanComponent>();

        while (query.MoveNext(out var station, out var plan))
        {
            if (now >= plan.NextSample)
            {
                plan.NextSample = now + SampleInterval;

                foreach (var (proto, state) in Quotas(plan))
                {
                    proto.Metric.Sample(Args(station, proto, state));
                }
            }

            if (now >= plan.NextEvaluation)
            {
                plan.NextEvaluation = now + TimeSpan.FromSeconds(_cfg.GetCVar(IS14CVars.GosplanPeriodSeconds));
                EvaluatePeriod(station, plan);
            }
        }
    }

    // ── Quota access ──────────────────────────────────────────────────────────

    /// <summary>
    /// The plan's quotas paired with their prototypes, in the order the board and the
    /// announcement show them, so a period always reads the same way round to round.
    /// </summary>
    public List<(PlanQuotaPrototype Proto, PlanQuotaState State)> Quotas(StationPlanComponent plan)
    {
        var quotas = new List<(PlanQuotaPrototype Proto, PlanQuotaState State)>(plan.Quotas.Count);

        foreach (var (quotaId, state) in plan.Quotas)
        {
            if (_prototypes.TryIndex<PlanQuotaPrototype>(quotaId, out var proto))
                quotas.Add((proto, state));
        }

        quotas.Sort((a, b) => a.Proto.Order.CompareTo(b.Proto.Order));
        return quotas;
    }

    private PlanMetricArgs Args(EntityUid station, PlanQuotaPrototype proto, PlanQuotaState state) =>
        new(EntityManager, station, proto, state.Metric);

    /// <summary>Where a quota stands right now, in the quota's own unit.</summary>
    public float GetCurrentValue(EntityUid station, PlanQuotaPrototype proto, PlanQuotaState state) =>
        proto.Metric.GetValue(Args(station, proto, state));

    public float GetFulfillment(EntityUid station, PlanQuotaPrototype proto, PlanQuotaState state)
    {
        if (proto.Target <= 0f)
            return 0f;

        return GetCurrentValue(station, proto, state) / proto.Target;
    }

    /// <summary>
    /// Scores every station's plan right now and starts a fresh period.
    /// Exists for admins and testing — nobody wants to wait out a period to see a payout.
    /// </summary>
    public void EvaluateNow()
    {
        var query = EntityQueryEnumerator<StationPlanComponent>();
        while (query.MoveNext(out var station, out var plan))
        {
            plan.NextEvaluation = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(IS14CVars.GosplanPeriodSeconds));
            EvaluatePeriod(station, plan);
        }
    }

    /// <summary>Human-readable snapshot of every station's plan, for the admin console.</summary>
    public string GetStatusReport()
    {
        var text = new StringBuilder();
        var query = EntityQueryEnumerator<StationPlanComponent>();
        var found = false;

        while (query.MoveNext(out var station, out var plan))
        {
            found = true;
            var left = (int)Math.Max(0, (plan.NextEvaluation - _timing.CurTime).TotalSeconds);
            text.AppendLine($"{ToPrettyString(station)}: period {plan.PeriodIndex}, {left}s left, banner: {(plan.BannerFund.Length > 0 ? plan.BannerFund : "-")}");

            foreach (var (proto, state) in Quotas(plan))
            {
                var fulfillment = GetFulfillment(station, proto, state);
                text.AppendLine($"  {proto.Fund,-20} {proto.ID,-28} {GetCurrentValue(station, proto, state):0.##}/{proto.Target:0.##} = {fulfillment * 100f:0}% (fails: {state.FailStreak})");
            }
        }

        return found ? text.ToString().TrimEnd() : "No station has a plan.";
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Scores every quota, pays out, hands the banner to the best department and
    /// sanctions anyone who failed twice running.
    /// </summary>
    private void EvaluatePeriod(EntityUid station, StationPlanComponent plan)
    {
        var cap = _cfg.GetCVar(IS14CVars.GosplanOverfulfillmentCap);
        var multiplier = _cfg.GetCVar(IS14CVars.GosplanPayoutMultiplier);
        var failThreshold = _cfg.GetCVar(IS14CVars.GosplanFailureThreshold);
        var overThreshold = _cfg.GetCVar(IS14CVars.GosplanOverfulfillmentThreshold);

        var lines = new List<string>();
        var failedFunds = new List<string>();
        var totalPayout = 0;
        var fulfillmentSum = 0f;
        var scored = 0;

        string? leaderFund = null;
        var leaderFulfillment = 0f;

        var quotas = Quotas(plan);

        foreach (var (proto, state) in quotas)
        {
            var fulfillment = GetFulfillment(station, proto, state);
            var paidFulfillment = Math.Clamp(fulfillment, 0f, cap);
            var payout = (int)(proto.Payout * paidFulfillment * multiplier);

            if (payout > 0 && _stationBank.TryChangeStationBalance(station, proto.Fund, payout, out var newBalance))
            {
                totalPayout += payout;

                if (_stationBank.GetStationAccount(station, proto.Fund) is { } account)
                {
                    RaiseLocalEvent(new EconomyTransactionEvent(account.AccountNumber, payout, newBalance,
                        Loc.GetString("economy-transaction-gosplan-funding", ("quota", Loc.GetString(proto.Name)))));
                }
            }

            if (fulfillment < failThreshold)
            {
                state.FailStreak++;
                failedFunds.Add(GetFundName(proto.Fund));
            }
            else
            {
                state.FailStreak = 0;
            }

            if (fulfillment >= overThreshold && fulfillment > leaderFulfillment)
            {
                leaderFulfillment = fulfillment;
                leaderFund = proto.Fund;
            }

            lines.Add(Loc.GetString("is14-gosplan-report-line",
                ("fund", GetFundName(proto.Fund)),
                ("quota", Loc.GetString(proto.Name)),
                ("percent", (int)MathF.Round(fulfillment * 100f)),
                ("payout", payout)));

            state.LastFulfillment = fulfillment;
            fulfillmentSum += fulfillment;
            scored++;
        }

        var leaderName = leaderFund != null ? GetFundName(leaderFund) : string.Empty;

        AnnounceReport(station, plan, lines, leaderFund, leaderName, quotas);

        plan.History.Add(new PlanPeriodEntry(
            plan.PeriodIndex,
            leaderName,
            scored == 0 ? 0f : fulfillmentSum / scored,
            totalPayout,
            failedFunds));

        var historyLimit = _cfg.GetCVar(IS14CVars.GosplanHistoryLength);
        while (plan.History.Count > historyLimit)
            plan.History.RemoveAt(0);

        // Start the metrics over last: payouts and sanctions above move money through the
        // same funds these quotas measure, and Gosplan's own money must not count as revenue.
        foreach (var (proto, state) in quotas)
        {
            proto.Metric.PeriodStarted(Args(station, proto, state));
        }

        plan.PeriodIndex++;
    }

    /// <summary>
    /// Builds and sends the end-of-period announcement, awarding the banner and
    /// applying sanctions along the way so all of it lands in one message.
    /// </summary>
    private void AnnounceReport(
        EntityUid station,
        StationPlanComponent plan,
        List<string> lines,
        string? leaderFund,
        string leaderName,
        List<(PlanQuotaPrototype Proto, PlanQuotaState State)> quotas)
    {
        var text = new StringBuilder();
        text.AppendLine(Loc.GetString("is14-gosplan-report-header", ("period", plan.PeriodIndex)));

        foreach (var line in lines)
            text.AppendLine(line);

        if (leaderFund != null)
        {
            AwardBanner(station, plan, leaderFund);
            text.AppendLine(Loc.GetString("is14-gosplan-report-banner", ("fund", leaderName)));
        }

        foreach (var (proto, state) in quotas)
        {
            if (state.FailStreak < 2)
                continue;

            var confiscated = ApplySanction(station, proto.Fund);
            text.AppendLine(Loc.GetString("is14-gosplan-report-sanction",
                ("fund", GetFundName(proto.Fund)),
                ("amount", confiscated)));

            // One sanction per streak, not one per period forever.
            state.FailStreak = 0;
        }

        _chat.DispatchStationAnnouncement(
            station,
            text.ToString().TrimEnd(),
            Loc.GetString("is14-gosplan-announcer"),
            colorOverride: Color.FromHex("#c9a53b"));
    }

    /// <summary>
    /// Hands the red banner to a department and tops up its bonus pool. The pool is
    /// what heads actually pay bonuses from, so winning the plan buys them goodwill
    /// they can hand out personally.
    /// </summary>
    private void AwardBanner(EntityUid station, StationPlanComponent plan, string fund)
    {
        plan.BannerFund = fund;
        plan.BannerPeriod = plan.PeriodIndex;

        var bonus = _cfg.GetCVar(IS14CVars.GosplanBannerBonus);
        if (bonus > 0)
            _stationBank.AddBonusPool(station, fund, bonus);
    }

    /// <summary>
    /// Confiscates a share of a department's budget after two failed periods.
    /// The credits leave the round entirely — this is one of the few real money sinks.
    /// </summary>
    private int ApplySanction(EntityUid station, string fund)
    {
        if (_stationBank.GetStationAccount(station, fund) is not { } account)
            return 0;

        var fraction = _cfg.GetCVar(IS14CVars.GosplanSanctionFraction);
        var amount = (int)(account.Balance * fraction);
        if (amount <= 0)
            return 0;

        if (!_stationBank.TryChangeStationBalance(station, fund, -amount, out var newBalance))
            return 0;

        RaiseLocalEvent(new EconomyTransactionEvent(account.AccountNumber, -amount, newBalance,
            Loc.GetString("economy-transaction-gosplan-sanction")));

        return amount;
    }

    private void AnnounceNewPeriod(EntityUid station, StationPlanComponent plan)
    {
        if (plan.Quotas.Count == 0)
            return;

        _chat.DispatchStationAnnouncement(
            station,
            Loc.GetString("is14-gosplan-plan-issued",
                ("period", plan.PeriodIndex),
                ("minutes", (int)(_cfg.GetCVar(IS14CVars.GosplanPeriodSeconds) / 60))),
            Loc.GetString("is14-gosplan-announcer"),
            colorOverride: Color.FromHex("#c9a53b"));
    }

    public string GetFundName(string fundProtoId)
    {
        return _prototypes.TryIndex<StationAccountPrototype>(fundProtoId, out var proto)
            ? proto.DisplayName
            : fundProtoId;
    }
}

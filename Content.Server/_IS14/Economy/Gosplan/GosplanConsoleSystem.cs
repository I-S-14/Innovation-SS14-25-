using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Economy.Gosplan;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.Gosplan;

/// <summary>
/// Feeds the soc-competition board. Progress moves continuously, so open boards are
/// refreshed on a timer rather than on every event.
/// </summary>
public sealed class GosplanConsoleSystem : EntitySystem
{
    [Dependency] private readonly GosplanSystem _gosplan = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<GosplanConsoleComponent>(GosplanConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
        });
    }

    private void OnOpened(Entity<GosplanConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        SendState(ent.Owner);
    }

    /// <summary>
    /// Clears the stored state once the last viewer leaves. Server-side UI state is sent
    /// to everyone in PVS and is never cleaned up on its own, so a board left alone would
    /// keep a full plan snapshot in its component for the rest of the round.
    /// </summary>
    private void OnClosed(Entity<GosplanConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!_ui.IsUiOpen(ent.Owner, GosplanConsoleUiKey.Key))
            _ui.SetUiState(ent.Owner, GosplanConsoleUiKey.Key, null);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<GosplanConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, GosplanConsoleUiKey.Key))
                SendState(uid);
        }
    }

    private void SendState(EntityUid console)
    {
        if (_station.GetOwningStation(console) is not { } station
            || !TryComp<StationPlanComponent>(station, out var plan))
        {
            _ui.SetUiState(console, GosplanConsoleUiKey.Key, new GosplanConsoleUiState { Active = false });
            return;
        }

        var cap = _cfg.GetCVar(IS14CVars.GosplanOverfulfillmentCap);
        var multiplier = _cfg.GetCVar(IS14CVars.GosplanPayoutMultiplier);

        var quotas = new List<PlanQuotaEntry>();

        // Already in board order.
        foreach (var (proto, state) in _gosplan.Quotas(plan))
        {
            var fulfillment = _gosplan.GetFulfillment(station, proto, state);

            quotas.Add(new PlanQuotaEntry
            {
                QuotaId = proto.ID,
                Name = Loc.GetString(proto.Name),
                Description = proto.Description != null ? Loc.GetString(proto.Description.Value) : string.Empty,
                FundId = proto.Fund,
                FundName = _gosplan.GetFundName(proto.Fund),
                CurrentText = proto.Unit.Format(_gosplan.GetCurrentValue(station, proto, state)),
                TargetText = proto.Unit.Format(proto.Target),
                Fulfillment = fulfillment,
                LastFulfillment = state.LastFulfillment,
                FailStreak = state.FailStreak,
                // Same formula the period scoring uses, so the board never promises
                // a number the payout won't match.
                ProjectedPayout = (int)(proto.Payout * Math.Clamp(fulfillment, 0f, cap) * multiplier),
                FullPayout = (int)(proto.Payout * multiplier),
            });
        }

        var secondsLeft = (int)Math.Max(0, (plan.NextEvaluation - _timing.CurTime).TotalSeconds);

        _ui.SetUiState(console, GosplanConsoleUiKey.Key, new GosplanConsoleUiState
        {
            Active = true,
            PeriodIndex = plan.PeriodIndex,
            SecondsLeft = secondsLeft,
            PeriodSeconds = _cfg.GetCVar(IS14CVars.GosplanPeriodSeconds),
            Quotas = quotas,
            // Newest period first reads better in a log.
            History = plan.History.AsEnumerable().Reverse().ToList(),
            BannerFundName = plan.BannerFund.Length > 0 ? _gosplan.GetFundName(plan.BannerFund) : string.Empty,
            BannerPeriodIndex = plan.BannerPeriod,
            BannerBonus = _cfg.GetCVar(IS14CVars.GosplanBannerBonus),
            FailThreshold = _cfg.GetCVar(IS14CVars.GosplanFailureThreshold),
            OverThreshold = _cfg.GetCVar(IS14CVars.GosplanOverfulfillmentThreshold),
            PayoutCap = cap,
        });
    }
}

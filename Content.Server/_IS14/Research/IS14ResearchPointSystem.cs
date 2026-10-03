// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Research;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// The station's research data ledger: who earned what, and what it can be spent on.
/// Every source device pays in here and the NIC console spends from here.
/// </summary>
public sealed class IS14ResearchPointSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    /// <summary>How far back the console's income figure looks.</summary>
    public static readonly TimeSpan IncomeWindow = TimeSpan.FromMinutes(5);

    /// <summary>Currency the shift's opening grant is paid in.</summary>
    private static readonly ProtoId<ResearchPointTypePrototype> StartupGrantType = "Science";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized);
    }

    /// <summary>
    /// Opening grant, so the department starts the shift with a decision rather than a wait.
    /// </summary>
    private void OnStationInitialized(StationInitializedEvent args)
    {
        var grant = _cfg.GetCVar(IS14CVars.ResearchStartupGrant);

        if (grant > 0)
            AddPoints(args.Station, StartupGrantType, grant);
    }

    /// <summary>
    /// Global knob on everything the instruments pay out. Spending and admin grants ignore it.
    /// </summary>
    public float PayoutMultiplier => _cfg.GetCVar(IS14CVars.ResearchPayoutMultiplier);

    /// <summary>
    /// Station that owns a device. Falls back to the only station in the round when the
    /// device is off-grid — a bench on a shuttle or in a test map still has to work.
    /// </summary>
    public EntityUid? ResolveStation(EntityUid source)
    {
        if (_station.GetOwningStation(source) is { } owning)
            return owning;

        var stations = _station.GetStations();
        return stations.Count == 1 ? stations.First() : null;
    }

    public int GetPoints(EntityUid station, ProtoId<ResearchPointTypePrototype> type)
    {
        return TryComp<IS14ResearchPointsComponent>(station, out var comp) && comp.Points.TryGetValue(type, out var amount)
            ? amount
            : 0;
    }

    public Dictionary<string, int> GetAllPoints(EntityUid station)
    {
        var result = new Dictionary<string, int>();

        if (!TryComp<IS14ResearchPointsComponent>(station, out var comp))
            return result;

        foreach (var (type, amount) in comp.Points)
        {
            result[type.Id] = amount;
        }

        return result;
    }

    /// <summary>What has come in over <see cref="IncomeWindow"/>, per currency.</summary>
    public Dictionary<string, int> GetRecentIncome(EntityUid station)
    {
        var result = new Dictionary<string, int>();

        if (!TryComp<IS14ResearchPointsComponent>(station, out var comp))
            return result;

        PruneIncome(comp);

        foreach (var record in comp.RecentIncome)
        {
            result.TryGetValue(record.PointType, out var running);
            result[record.PointType] = running + record.Amount;
        }

        return result;
    }

    /// <summary>
    /// Credits the station. Negative amounts are allowed but never take a balance below zero.
    /// </summary>
    public void AddPoints(EntityUid station, ProtoId<ResearchPointTypePrototype> type, int amount)
    {
        if (amount == 0)
            return;

        var comp = EnsureComp<IS14ResearchPointsComponent>(station);

        comp.Points.TryGetValue(type, out var current);
        comp.Points[type] = Math.Max(0, current + amount);

        if (amount > 0)
        {
            comp.RecentIncome.Add(new IS14ResearchIncomeRecord(_timing.CurTime, type.Id, amount));
            PruneIncome(comp);
        }

        var ev = new IS14ResearchPointsChangedEvent(station);
        RaiseLocalEvent(station, ref ev);
    }

    /// <summary>
    /// Pays for a measurement made by <paramref name="source"/>, resolving its station.
    /// Returns false when there is no station to pay.
    /// </summary>
    public bool TryAward(EntityUid source, ProtoId<ResearchPointTypePrototype> type, int amount)
    {
        if (ResolveStation(source) is not { } station)
            return false;

        AddPoints(station, type, amount);
        return true;
    }

    public bool CanAfford(EntityUid station, IReadOnlyDictionary<ProtoId<ResearchPointTypePrototype>, int> costs)
    {
        foreach (var (type, price) in costs)
        {
            if (GetPoints(station, type) < price)
                return false;
        }

        return true;
    }

    public bool TrySpend(EntityUid station, IReadOnlyDictionary<ProtoId<ResearchPointTypePrototype>, int> costs)
    {
        if (!CanAfford(station, costs))
            return false;

        var comp = EnsureComp<IS14ResearchPointsComponent>(station);

        foreach (var (type, price) in costs)
        {
            comp.Points.TryGetValue(type, out var current);
            comp.Points[type] = Math.Max(0, current - price);
        }

        var ev = new IS14ResearchPointsChangedEvent(station);
        RaiseLocalEvent(station, ref ev);
        return true;
    }

    /// <summary>
    /// Looks up how many times a profile has been measured and remembers this measurement.
    /// This is the novelty rule: the archive, not the device, decides what a result is worth.
    /// </summary>
    public int RecordMeasurement(EntityUid station, string profileKey)
    {
        var archive = EnsureComp<IS14ResearchArchiveComponent>(station);

        archive.Measurements.TryGetValue(profileKey, out var prior);
        archive.Measurements[profileKey] = prior + 1;

        return prior;
    }

    /// <summary>How many times a profile was measured, without recording anything.</summary>
    public int PeekMeasurement(EntityUid station, string profileKey)
    {
        return TryComp<IS14ResearchArchiveComponent>(station, out var archive)
               && archive.Measurements.TryGetValue(profileKey, out var prior)
            ? prior
            : 0;
    }

    private void PruneIncome(IS14ResearchPointsComponent comp)
    {
        var cutoff = _timing.CurTime - IncomeWindow;
        comp.RecentIncome.RemoveAll(r => r.Time < cutoff);
    }
}

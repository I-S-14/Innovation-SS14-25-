// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Anomaly;
using Content.Server.Anomaly.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Research;
using Content.Shared.Anomaly.Components;
using Content.Shared.Radio;
using Content.Shared.Research.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// Replaces the upstream passive drip with something a department has to earn: the research
/// server's own trickle and the anomalous vessel's endless points are cut off, and vessels
/// instead pay for new anomaly types and for severity records.
/// </summary>
/// <remarks>
/// The cut is one chokepoint — <see cref="ResearchServerGetPointsPerSecondEvent"/> — handled
/// after the systems that fill it in, so nothing upstream had to be edited. It is behind a
/// cvar because the old R&amp;D console still exists and someone may want it fed.
/// </remarks>
public sealed class IS14AnomalyVesselSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14RecordSystem _records = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly IS14ExperimentSystem _experiment = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Channel the vessel files its findings on.</summary>
    private static readonly ProtoId<RadioChannelPrototype> Channel = "Science";

    public override void Initialize()
    {
        base.Initialize();

        // Upstream raises this on every research client and then, last, on the server itself.
        // By that point the running total holds everything the point sources and the vessels
        // put in, so one handler here zeroes the lot.
        //
        // It has to be this component: the engine allows a single subscription per component
        // and event, and upstream already owns ResearchPointSourceComponent and
        // AnomalyVesselComponent for this event. `after` covers the case of a server that is
        // also a point source, where both handlers run on the same raise.
        SubscribeLocalEvent<ResearchServerComponent, ResearchServerGetPointsPerSecondEvent>(
            OnServerPoints, after: new[] { typeof(ResearchSystem), typeof(AnomalySystem) });
    }

    private bool CutPassive => _cfg.GetCVar(IS14CVars.ResearchCutUpstreamPassive);

    private void OnServerPoints(
        Entity<ResearchServerComponent> ent,
        ref ResearchServerGetPointsPerSecondEvent args)
    {
        if (CutPassive)
            args.Points = 0;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AnomalyVesselComponent>();
        while (query.MoveNext(out var uid, out var vessel))
        {
            if (vessel.Anomaly is not { } anomaly || !Exists(anomaly))
                continue;

            var research = EnsureComp<IS14AnomalyResearchComponent>(uid);

            if (_timing.CurTime < research.NextSample)
                continue;

            research.NextSample = _timing.CurTime + research.Interval;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            Read((uid, research), anomaly);
        }
    }

    private void Read(Entity<IS14AnomalyResearchComponent> ent, EntityUid anomaly)
    {
        if (_points.ResolveStation(ent.Owner) is not { } station
            || !TryComp<AnomalyComponent>(anomaly, out var comp))
        {
            return;
        }

        var kind = MetaData(anomaly).EntityPrototype?.ID ?? "unknown";

        if (ent.Comp.Filed != anomaly)
        {
            ent.Comp.Filed = anomaly;
            File(ent, station, $"anomaly:{kind}", ent.Comp.StabilisationValue);
        }

        // Severity is a 0..1 figure; records are kept in percent so they read like numbers.
        var share = _records.TryRecord(station, $"anomaly:severity:{kind}", comp.Severity * 100f);

        if (share > 0f)
            Pay(ent, station, (int) MathF.Round(ent.Comp.RecordValue * share));
    }

    /// <summary>Files a first-of-its-kind result: the novelty rule prices repeats, as ever.</summary>
    private void File(Entity<IS14AnomalyResearchComponent> ent, EntityUid station, string key, int baseValue)
    {
        var prior = _points.RecordMeasurement(station, key);
        var payout = (int) MathF.Round(baseValue * _experiment.NoveltyMultiplier(station, prior));

        Pay(ent, station, payout, prior);
    }

    private void Pay(Entity<IS14AnomalyResearchComponent> ent, EntityUid station, int amount, int prior = 0)
    {
        var payout = Math.Max(1, (int) MathF.Round(
            amount
            * _points.PayoutMultiplier
            * _modifiers.GetPayoutMultiplier(station, ent.Comp.PointType)));

        _points.AddPoints(station, ent.Comp.PointType, payout);

        _experiment.ReportResult(
            ent.Owner,
            null,
            new IS14MeasurementResult
            {
                Payout = payout,
                PointType = ent.Comp.PointType,
                PriorCount = prior,
            },
            Channel);
    }
}

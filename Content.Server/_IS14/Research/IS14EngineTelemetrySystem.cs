// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Research;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// Reads the station's power generation and pays for records. The engineering half of
/// industrial data — see <see cref="IS14EngineTelemetryComponent"/> for why it is records and
/// not uptime.
/// </summary>
public sealed class IS14EngineTelemetrySystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14RecordSystem _records = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly IS14ExperimentSystem _experiment = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IS14EngineTelemetryComponent>();
        while (query.MoveNext(out var uid, out var telemetry))
        {
            if (_timing.CurTime < telemetry.NextSample)
                continue;

            telemetry.NextSample = _timing.CurTime + telemetry.Interval;

            if (!this.IsPowered(uid, EntityManager))
                continue;

            Sample((uid, telemetry));
        }
    }

    private void Sample(Entity<IS14EngineTelemetryComponent> ent)
    {
        if (_points.ResolveStation(ent.Owner) is not { } station)
            return;

        var watts = Generation(station);

        if (watts < ent.Comp.MinimumWatts)
            return;

        var share = _records.TryRecord(station, ent.Comp.RecordKey, watts, ent.Comp.FullShare);

        if (share <= 0f)
            return;

        var payout = Math.Max(1, (int) MathF.Round(
            ent.Comp.BaseValue
            * share
            * _points.PayoutMultiplier
            * _modifiers.GetPayoutMultiplier(station, ent.Comp.PointType)));

        _points.AddPoints(station, ent.Comp.PointType, payout);

        var result = new IS14MeasurementResult
        {
            Payout = payout,
            PointType = ent.Comp.PointType,
            // A record is new data by definition, so the novelty label reads "new".
            PriorCount = 0,
            ProfileKey = ent.Comp.RecordKey,
        };

        _experiment.ReportResult(ent.Owner, null, result, ent.Comp.Channel);

        if (ent.Comp.Channel is { } channel)
        {
            Log.Debug($"Engine telemetry on {ToPrettyString(station)}: record {watts:F0} W, paid {payout} ({channel}).");
        }
    }

    /// <summary>Everything the station is generating right now, in watts.</summary>
    public float Generation(EntityUid station)
    {
        var total = 0f;
        var query = EntityQueryEnumerator<PowerSupplierComponent>();

        while (query.MoveNext(out var uid, out var supplier))
        {
            if (_station.GetOwningStation(uid) != station)
                continue;

            total += supplier.CurrentSupply;
        }

        return total;
    }
}

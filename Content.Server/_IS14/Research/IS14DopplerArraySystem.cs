// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server.Pinpointer;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._IS14.Explosion;
using Content.Shared._IS14.Research;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// Tachyon-doppler array. Measures explosions in the direction it faces, logs each reading as a
/// numbered record, and pays military data for beating the station's own record.
/// </summary>
/// <remarks>
/// Rebuilt after tgstation's ordnance loop. Three things make it a machine rather than a payout
/// box: it is directional, so the array has to be aimed at a test site; every reading becomes a
/// record that can be printed on paper and filed; and the money is in records, so "the same bomb
/// again" is worth almost nothing while "a bigger bomb" is the whole job.
/// </remarks>
public sealed class IS14DopplerArraySystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14ExplosionQueuedEvent>(OnExplosion);

        Subs.BuiEvents<IS14DopplerArrayComponent>(IS14DopplerArrayUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>((uid, comp, _) => UpdateUi((uid, comp)));
            subs.Event<IS14DopplerPrintRecordMessage>(OnPrintRecord);
            subs.Event<IS14DopplerDeleteRecordMessage>(OnDeleteRecord);
        });
    }

    #region Measuring

    private void OnExplosion(IS14ExplosionQueuedEvent args)
    {
        // The closest array that can actually see the blast takes the reading; building a second
        // one must not double the payout. An array the blast went off on top of only gets the
        // reading if no properly placed array saw it, so one badly sited sensor cannot spoil a
        // legitimate test.
        Entity<IS14DopplerArrayComponent>? best = null;
        Entity<IS14DopplerArrayComponent>? tooClose = null;
        var bestDistance = float.MaxValue;
        var tooCloseDistance = float.MaxValue;

        var query = EntityQueryEnumerator<IS14DopplerArrayComponent>();
        while (query.MoveNext(out var uid, out var array))
        {
            if (!this.IsPowered(uid, EntityManager))
                continue;

            var xform = Transform(uid);

            if (xform.MapID != args.Epicenter.MapId)
                continue;

            var toEpicenter = args.Epicenter.Position - _xform.GetWorldPosition(uid);
            var distance = toEpicenter.Length();

            if (distance > array.MaxDistance || args.TotalIntensity < array.MinIntensity)
                continue;

            if (!InFieldOfView((uid, array), toEpicenter))
                continue;

            if (distance < array.MinDistance)
            {
                if (distance < tooCloseDistance)
                {
                    tooCloseDistance = distance;
                    tooClose = (uid, array);
                }

                continue;
            }

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = (uid, array);
        }

        if (best is { } target)
            Measure(target, args, bestDistance);
        else if (tooClose is { } witness)
            Measure(witness, args, tooCloseDistance);
    }

    /// <summary>
    /// Whether the epicentre lies inside the array's sensor cone. Directional on purpose: an
    /// array nobody aimed measures nothing, which is what makes the test site a real place.
    /// </summary>
    private bool InFieldOfView(Entity<IS14DopplerArrayComponent> ent, Vector2 toEpicenter)
    {
        if (toEpicenter.LengthSquared() <= 0.01f)
            return true;

        var facing = _xform.GetWorldRotation(ent.Owner).ToWorldVec();
        var cosine = Vector2.Dot(Vector2.Normalize(facing), Vector2.Normalize(toEpicenter));
        var angle = MathF.Acos(Math.Clamp(cosine, -1f, 1f)) * (180f / MathF.PI);

        return angle <= ent.Comp.FieldOfView;
    }

    private void Measure(Entity<IS14DopplerArrayComponent> ent, IS14ExplosionQueuedEvent args, float distance)
    {
        var array = ent.Comp;

        var record = BuildRecord(ent, args, distance);
        var tooClose = distance < array.MinDistance;

        record.TooClose = tooClose;

        if (!tooClose && _points.ResolveStation(ent.Owner) is { } station)
            Pay(ent, station, ref record);

        array.Records.Add(record);

        while (array.Records.Count > array.MaxRecords)
        {
            array.Records.RemoveAt(0);
        }

        Announce(ent, record);
        UpdateUi(ent);
    }

    /// <summary>
    /// Turns the raw blast into the numbers a readout quotes. Radii are estimates: the engine
    /// hands out total intensity and falloff, and a cone of that volume has a known reach.
    /// </summary>
    private IS14OrdnanceRecord BuildRecord(
        Entity<IS14DopplerArrayComponent> ent,
        IS14ExplosionQueuedEvent args,
        float distance)
    {
        var slope = MathF.Max(0.1f, args.Slope);
        var total = MathF.Max(0f, args.TotalIntensity);

        // Intensity falls off by `slope` per tile, so an uncapped blast is a cone of height
        // `peak` and radius `peak / slope`; its volume is the total intensity.
        var peak = MathF.Cbrt(3f * total * slope * slope / MathF.PI);
        float shockwave;

        if (args.MaxTileIntensity > 0f && peak > args.MaxTileIntensity)
        {
            // Capped blast: the centre flattens off at the cap and the energy spreads outwards,
            // which is why a capped bomb covers more ground than its peak alone suggests.
            peak = args.MaxTileIntensity;
            shockwave = MathF.Sqrt(2f * total / (MathF.PI * peak));
        }
        else
        {
            shockwave = peak / slope;
        }

        // Zones by share of the peak, not by absolute damage: total destruction where intensity
        // stays above two thirds of the peak, heavy damage above a third, shockwave to the edge.
        var epicenter = shockwave * (1f / 3f);
        var outer = shockwave * (2f / 3f);

        return new IS14OrdnanceRecord
        {
            Number = ent.Comp.RecordNumber++,
            Timestamp = _timing.CurTime.ToString(@"hh\:mm\:ss"),
            Coordinates = $"{args.Epicenter.Position.X:F0}, {args.Epicenter.Position.Y:F0}",
            Location = NearestBeacon(args.Epicenter),
            TotalIntensity = args.TotalIntensity,
            Slope = args.Slope,
            MaxTileIntensity = args.MaxTileIntensity,
            EpicenterRadius = epicenter,
            OuterRadius = outer,
            ShockwaveRadius = shockwave,
            Distance = distance,
        };
    }

    /// <summary>
    /// Name of the nearest station beacon, or a fallback: a beacon without a label hands back
    /// nothing, and an unnamed place on the radio is worse than "unknown sector".
    /// </summary>
    private string NearestBeacon(MapCoordinates coordinates)
    {
        var beacon = _navMap.GetNearestBeaconString(coordinates, onlyName: true);

        return string.IsNullOrWhiteSpace(beacon)
            ? Loc.GetString("is14-doppler-location-unknown")
            : beacon;
    }

    /// <summary>
    /// Pays for the reading. A blast that beats the station's best of its type pays in full; one
    /// that does not is a confirmation and pays a fraction, which is what stops charge spam.
    /// </summary>
    private void Pay(Entity<IS14DopplerArrayComponent> ent, EntityUid station, ref IS14OrdnanceRecord record)
    {
        var array = ent.Comp;
        var records = EnsureComp<IS14OrdnanceRecordsComponent>(station);

        var beatsRecord = record.TotalIntensity > records.BestIntensity;

        if (beatsRecord)
            records.BestIntensity = record.TotalIntensity;

        var scale = Math.Clamp(
            MathF.Sqrt(record.TotalIntensity / MathF.Max(1f, array.ReferenceIntensity)),
            0.2f,
            array.MaxScale);

        var fraction = beatsRecord ? 1f : array.RepeatFraction;

        var payout = Math.Max(1, (int) MathF.Round(
            array.BaseValue
            * scale
            * fraction
            * _points.PayoutMultiplier
            * _modifiers.GetPayoutMultiplier(station, array.PointType)));

        _points.AddPoints(station, array.PointType, payout);

        record.Payout = payout;
        record.Record = beatsRecord;
    }

    #endregion

    #region Readout

    /// <summary>Reads the result out over the radio, the way tg's array states it aloud.</summary>
    private void Announce(Entity<IS14DopplerArrayComponent> ent, IS14OrdnanceRecord record)
    {
        if (ent.Comp.Channel is not { } channel)
            return;

        var header = Loc.GetString("is14-doppler-readout-header", ("location", record.Location));

        var radii = Loc.GetString("is14-doppler-readout-radii",
            ("epicenter", record.EpicenterRadius.ToString("F1")),
            ("outer", record.OuterRadius.ToString("F1")),
            ("shockwave", record.ShockwaveRadius.ToString("F1")),
            ("intensity", (int) record.TotalIntensity));

        // Nothing about records or payouts: that belongs in the array's own log, not on the air.
        var readout = record.TooClose
            ? $"{header} {radii} {Loc.GetString("is14-doppler-readout-too-close")}"
            : $"{header} {radii}";

        Say(ent.Owner, channel, readout);
    }

    private void Say(EntityUid source, string channel, string message)
    {
        _radio.SendRadioMessage(source, message, channel, source, escapeMarkup: false);
    }

    #endregion

    #region Interface

    private void OnPrintRecord(Entity<IS14DopplerArrayComponent> ent, ref IS14DopplerPrintRecordMessage args)
    {
        if (!this.IsPowered(ent.Owner, EntityManager))
            return;

        var number = args.Number;
        var index = ent.Comp.Records.FindIndex(entry => entry.Number == number);

        if (index < 0)
            return;

        var found = ent.Comp.Records[index];

        var paper = Spawn(ent.Comp.ProtocolPrototype, Transform(ent.Owner).Coordinates);

        _paper.SetContent(paper, BuildProtocol(found));
        _popup.PopupEntity(Loc.GetString("is14-doppler-protocol-printed"), ent.Owner, args.Actor);
    }

    private void OnDeleteRecord(Entity<IS14DopplerArrayComponent> ent, ref IS14DopplerDeleteRecordMessage args)
    {
        var number = args.Number;
        ent.Comp.Records.RemoveAll(entry => entry.Number == number);
        UpdateUi(ent);
    }

    /// <summary>
    /// The printed protocol: an ordinary sheet of paper, so it can be stamped, signed and sent
    /// to the Academy like any other report.
    /// </summary>
    private string BuildProtocol(IS14OrdnanceRecord record)
    {
        return Loc.GetString("is14-doppler-protocol-body",
            ("number", record.Number),
            ("timestamp", record.Timestamp),
            ("location", record.Location),
            ("coordinates", record.Coordinates),
            ("distance", (int) record.Distance),
            ("intensity", (int) record.TotalIntensity),
            ("slope", record.Slope.ToString("F1")),
            ("peak", (int) record.MaxTileIntensity),
            ("epicenter", record.EpicenterRadius.ToString("F1")),
            ("outer", record.OuterRadius.ToString("F1")),
            ("shockwave", record.ShockwaveRadius.ToString("F1")),
            ("payout", record.Payout));
    }

    private void UpdateUi(Entity<IS14DopplerArrayComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, IS14DopplerArrayUiKey.Key))
            return;

        var state = new IS14DopplerArrayUiState
        {
            Records = new List<IS14OrdnanceRecord>(ent.Comp.Records),
            Facing = Loc.GetString($"is14-doppler-facing-{_xform.GetWorldRotation(ent.Owner).GetCardinalDir().ToString().ToLowerInvariant()}"),
        };

        if (_points.ResolveStation(ent.Owner) is { } station
            && TryComp<IS14OrdnanceRecordsComponent>(station, out var records))
        {
            state.BestIntensity = records.BestIntensity;
        }

        _ui.SetUiState(ent.Owner, IS14DopplerArrayUiKey.Key, state);
    }

    #endregion
}

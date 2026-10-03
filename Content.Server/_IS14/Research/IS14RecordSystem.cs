// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Research;

namespace Content.Server._IS14.Research;

/// <summary>
/// Keeps the station's records and decides what beating one is worth. Shared by engine
/// telemetry, atmospheric samples and anomaly vessels, so "a record" means the same thing
/// everywhere and the rate is tuned in one place.
/// </summary>
public sealed class IS14RecordSystem : EntitySystem
{
    /// <summary>
    /// The first record of a kind is worth this share of a full payout: without it, the very
    /// first measurement — which has nothing to beat — would pay nothing at all.
    /// </summary>
    public const float FirstRecordShare = 0.5f;

    /// <summary>Best figure the station has for this record, or null when it has none.</summary>
    public float? Peak(EntityUid? station, string key)
    {
        return station != null
               && TryComp<IS14StationRecordsComponent>(station, out var records)
               && records.Best.TryGetValue(key, out var best)
            ? best
            : null;
    }

    /// <summary>
    /// Offers a figure as a record. Returns how much of a full payout it earned: zero when it
    /// failed to beat the standing record, up to one when it beat it outright.
    /// </summary>
    /// <remarks>
    /// Scaled by how much the record moved, so scraping past the old figure by a percent is
    /// worth a fraction of doubling it. That keeps a department from farming a record by
    /// nudging the same number upwards all shift.
    /// </remarks>
    public float TryRecord(EntityUid station, string key, float value, float fullShare = 2f)
    {
        if (value <= 0f)
            return 0f;

        var records = EnsureComp<IS14StationRecordsComponent>(station);

        if (!records.Best.TryGetValue(key, out var best))
        {
            records.Best[key] = value;
            return FirstRecordShare;
        }

        if (value <= best)
            return 0f;

        records.Best[key] = value;

        // `fullShare` is the ratio that counts as a full result: twice the old record by default.
        var growth = (value - best) / best;
        return Math.Clamp(growth / Math.Max(0.01f, fullShare - 1f), 0.05f, 1f);
    }
}

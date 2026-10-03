// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;

namespace Content.Shared._IS14.Explosion;

/// <summary>
/// Broadcast when an explosion is queued, with the numbers instruments need to measure it.
/// </summary>
/// <remarks>
/// Raised from upstream's ExplosionSystem.QueueExplosion — there is no other hook that
/// carries the epicentre and the total intensity together. Kept generic on purpose: the
/// doppler array is the first listener, but blast telemetry is equally useful for
/// engineering records, admin stats and antag objectives.
/// </remarks>
public sealed class IS14ExplosionQueuedEvent : EntityEventArgs
{
    public readonly MapCoordinates Epicenter;
    public readonly string ExplosionType;
    public readonly float TotalIntensity;
    public readonly float Slope;

    /// <summary>Ceiling on a single tile's intensity, which is what caps a blast's peak.</summary>
    public readonly float MaxTileIntensity;

    public readonly EntityUid? Cause;

    public IS14ExplosionQueuedEvent(
        MapCoordinates epicenter,
        string explosionType,
        float totalIntensity,
        float slope,
        float maxTileIntensity,
        EntityUid? cause)
    {
        Epicenter = epicenter;
        ExplosionType = explosionType;
        TotalIntensity = totalIntensity;
        Slope = slope;
        MaxTileIntensity = maxTileIntensity;
        Cause = cause;
    }
}

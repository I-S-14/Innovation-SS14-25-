// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Server.Station.Components;
using System.Diagnostics;
using System.Numerics;
using Content.Server.StationEvents.Components;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Station event component for spawning entities in space around a station.
/// </summary>
/// <remarks>
/// Commonly used to spawn antags, like the space ninja.
/// </remarks>
/// <seealso cref="SpaceSpawnRuleComponent"/>
public sealed partial class SpaceSpawnRule : StationEventSystem<SpaceSpawnRuleComponent>
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;

    /// <summary>
    /// Tries to find a position to spawn in space around a given station.
    /// </summary>
    /// <remarks>
    /// Rough flow:
    /// Select an eligible station.
    /// Find the bounding box of the largest grid on the selected station.
    /// Pick a random starting angle ANG.
    /// Create a circle some distance from the bounding box extremities.
    /// Check points on that circle evenly spaced from ANG in a random order for nearby grids.
    /// If there are any nearby, continue to the next point.
    /// Otherwise, return that point.
    /// </remarks>
    protected override void Added(Entity<SpaceSpawnRuleComponent, GameRuleComponent> ent, ref GameRuleAddedEvent args)
    {
        base.Added(ent, ref args);

        var spaceSpawn = ent.Comp1;

        if (!Station.TryGetRandomStation<StationEventEligibleComponent>(out var station))
        {
            Sawmill.Warning($"No eligible stations, cannot pick location for {ToPrettyString(ent):rule}");
            ForceEndSelf((ent.Owner, ent.Comp2));
            return;
        }

        // find a station grid
        var gridUid = Station.GetLargestGrid(station.Value.Owner);
        if (gridUid == null || !_gridQuery.TryComp(gridUid, out var grid))
        {
            Sawmill.Warning($"Chosen station has no grids, cannot pick location for {ToPrettyString(ent):rule}");
            ForceEndSelf((ent.Owner, ent.Comp2));
            return;
        }

        Debug.Assert(spaceSpawn.MaxAttempts > 0, $"Rule {ToPrettyString(ent):rule} has a non-positive MaxAttempts value");
        var maxAttempts = int.Max(1, spaceSpawn.MaxAttempts);

        // figure out its AABB size and use that as a guide to how far the spawner should be
        var size = grid.LocalAABB.Size.Length() / 2;
        var distance = size + spaceSpawn.SpawnDistance;

        // create the spawner!
        var xform = Transform(gridUid.Value);
        var position = _transform.GetWorldPosition(xform) + grid.LocalAABB.Center + location; // CorvaxGoob edit
        comp.Coords = new MapCoordinates(position, xform.MapID);
        Sawmill.Info($"Picked location {comp.Coords} for {ToPrettyString(uid):rule}");
    }

    [SubscribeLocalEvent]
    private void OnSelectLocation(Entity<SpaceSpawnRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.Coords is { } coords)
            args.Coordinates.Add(coords);
    }
}

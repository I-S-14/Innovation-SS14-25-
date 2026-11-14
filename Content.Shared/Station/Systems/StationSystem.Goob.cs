// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Station.Components;

namespace Content.Shared.Station.Systems;

public abstract partial class StationSystem
{
    // Goobstation start
    public HashSet<EntityUid> GoobGetAllStationGrids()
    {
        // Collect all grids owned by stations
        var grids = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out _, out var data))
        {
            // Add to the list of grids
            grids.UnionWith(data.Grids);
        }

        return grids;
    }
    // Goobstation end
}

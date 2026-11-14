using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Content.Shared.Spawners.Components;
using Content.Shared.Station.Systems;
using Robust.Shared.Map;

namespace Content.Shared.Spawners.EntitySystems;

public sealed partial class SpawnPointSystem
{
    private void AddRetainedJobSpawnPoints(PlayerSpawningEvent args, List<EntityCoordinates> positions)
    {
        if (positions.Count != 0 || _gameTicker.RunLevel == GameRunLevel.InRound || args.Job == null
            || !ProtoMan.TryIndex<JobPrototype>(args.Job.Value, out var job) || job.SpawnPointJob == null)
            return;

        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var point, out var xform))
        {
            if (point.SpawnType != SpawnPointType.Job || point.Job != job.SpawnPointJob)
                continue;
            if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                continue;
            positions.Add(xform.Coordinates);
        }
    }
}

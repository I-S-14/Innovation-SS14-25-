using System.Numerics;
using Content.Server.Decals;
using Content.Shared.Decals;
using Content.Shared.GameTicking;
using Robust.Shared.GameStates;

namespace Content.Server._CorvaxGoob.CorvaxFootPrint.CleanableDecals;

/// <summary>
/// Caches cleanable decal positions per network chunk. The cache is invalidated on every decal mutation.
/// </summary>
public sealed partial class CleanableDecalTrackerSystem : EntitySystem
{
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private ChunkEntitySystem _chunks = default!;
    [Dependency] private EntityQuery<DecalChunkComponent> _decalQuery;

    private readonly Dictionary<EntityUid, Dictionary<ushort, Vector2>> _cleanable = new();
    private readonly List<DecalIndex> _removing = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DecalChunkComponent, DecalChunkChangedEvent>(OnChanged);
        SubscribeLocalEvent<DecalChunkComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnChanged(Entity<DecalChunkComponent> ent, ref DecalChunkChangedEvent args) => _cleanable.Remove(ent.Owner);
    private void OnShutdown(EntityUid uid, DecalChunkComponent comp, ComponentShutdown args) => _cleanable.Remove(uid);
    private void OnRestart(RoundRestartCleanupEvent args) => _cleanable.Clear();

    private Dictionary<ushort, Vector2> GetCleanable(Entity<ChunkEntityComponent, DecalChunkComponent> chunk)
    {
        if (_cleanable.TryGetValue(chunk.Owner, out var cached))
            return cached;
        cached = new Dictionary<ushort, Vector2>();
        foreach (var (id, decal) in chunk.Comp2.Decals)
        {
            if (decal.Cleanable)
                cached.Add(id, decal.Coordinates);
        }
        _cleanable.Add(chunk.Owner, cached);
        return cached;
    }

    public bool TryFindNearest(EntityUid grid, Vector2 localPos, float range, out Vector2 target, out DecalIndex decalId)
    {
        target = default;
        decalId = default;
        var found = false;
        var bestDistance = range * range;
        var bounds = new Box2(localPos - new Vector2(range + 1f), localPos + new Vector2(range + 1f));
        foreach (var chunk in _chunks.GetChunksIntersecting(grid, bounds, _decalQuery))
        {
            foreach (var (id, pos) in GetCleanable(chunk))
            {
                var center = pos + new Vector2(0.5f);
                var distance = Vector2.DistanceSquared(localPos, center);
                if (distance > bestDistance)
                    continue;
                bestDistance = distance;
                target = center;
                decalId = new DecalIndex(chunk.Comp1.Chunk, id);
                found = true;
            }
        }
        return found;
    }

    public int CleanInRange(EntityUid grid, Vector2 localPos, float radius)
    {
        _removing.Clear();
        var radiusSquared = radius * radius;
        var bounds = new Box2(localPos - new Vector2(radius + 1f), localPos + new Vector2(radius + 1f));
        foreach (var chunk in _chunks.GetChunksIntersecting(grid, bounds, _decalQuery))
        {
            foreach (var (id, pos) in GetCleanable(chunk))
            {
                if (Vector2.DistanceSquared(localPos, pos + new Vector2(0.5f)) <= radiusSquared)
                    _removing.Add(new DecalIndex(chunk.Comp1.Chunk, id));
            }
        }
        var removed = 0;
        foreach (var id in _removing)
        {
            if (_decals.RemoveDecal(grid, id))
                removed++;
        }
        return removed;
    }
}

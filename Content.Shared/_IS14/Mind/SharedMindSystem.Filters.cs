using Content.Shared.EntityConditions;
using Content.Shared.Mind.Filters;
using Robust.Shared.Random;

namespace Content.Shared.Mind;

public abstract partial class SharedMindSystem
{
    /// <summary>Apply legacy exclusions before random selection, after the modern pool's conditions.</summary>
    public Entity<MindComponent>? PickFromPool(
        Content.Shared.Objectives.Systems.IMindPool pool,
        EntityUid? exclude,
        EntityCondition[] conditions,
        IReadOnlyCollection<MindFilter> filters)
    {
        if (filters.Count == 0)
            return PickFromPool(pool, exclude, conditions);

        _pickingMinds.Clear();
        pool.FindMinds(_pickingMinds, _dependency, exclude, conditions);
        foreach (var filter in filters)
            _pickingMinds.RemoveWhere(mind => filter.Filter(mind, exclude, EntityManager, this));

        return _pickingMinds.Count == 0 ? null : _random.Pick(_pickingMinds);
    }
}

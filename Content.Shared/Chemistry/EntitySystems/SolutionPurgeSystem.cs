// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.Timing;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class SolutionPurgeSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionPurgeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SolutionPurgeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextPurgeTime = _timing.CurTime + ent.Comp.Duration;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        //IS14-change: support both new solution entities and retained named item solutions.
        var query = EntityQueryEnumerator<SolutionPurgeComponent>();
        while (query.MoveNext(out var uid, out var purge))
        {
            if (_timing.CurTime < purge.NextPurgeTime)
                continue;

            Entity<SolutionComponent> target;
            if (purge.SolutionName != null)
            {
                if (!_solutionContainer.TryGetSolution(uid, purge.SolutionName, out var named))
                    continue;
                target = named.Value;
            }
            else if (TryComp<SolutionComponent>(uid, out var solution))
                target = (uid, solution);
            else
                continue;

            // timer ignores if it's empty, it's just a fixed cycle
            purge.NextPurgeTime += purge.Duration;
            // Needs to be networked and dirtied so that the client can reroll it during prediction
            Dirty(uid, purge);

            _solutionContainer.SplitSolutionWithout(target,
                purge.Quantity,
                purge.Preserve.ToArray());
        }
    }
}

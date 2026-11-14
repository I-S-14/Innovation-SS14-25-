using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class SolutionRegenerationSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionRegenerationComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SolutionRegenerationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextRegenTime = _timing.CurTime + ent.Comp.Duration;

        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        //IS14-change: retain named item solutions alongside new solution entities.
        var query = EntityQueryEnumerator<SolutionRegenerationComponent>();
        while (query.MoveNext(out var uid, out var regen))
        {
            if (_timing.CurTime < regen.NextRegenTime)
                continue;

            Entity<SolutionComponent> target;
            if (regen.SolutionName != null)
            {
                if (!_solutionContainer.TryGetSolution(uid, regen.SolutionName, out var named))
                    continue;
                target = named.Value;
            }
            else if (TryComp<SolutionComponent>(uid, out var solution))
                target = (uid, solution);
            else
                continue;

            // timer ignores if its full, it's just a fixed cycle
            regen.NextRegenTime += regen.Duration;
            // Needs to be networked and dirtied so that the client can reroll it during prediction
            Dirty(uid, regen);
            var amount = FixedPoint2.Min(target.Comp.Solution.AvailableVolume, regen.Generated.Volume);
            if (amount <= FixedPoint2.Zero)
                continue;

            // Don't bother cloning and splitting if adding the whole thing
            var generated = amount == regen.Generated.Volume
                ? regen.Generated
                : regen.Generated.Clone().SplitSolution(amount);

            _solutionContainer.TryAddSolution(target, generated);
        }
    }
}

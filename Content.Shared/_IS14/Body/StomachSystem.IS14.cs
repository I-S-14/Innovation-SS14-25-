// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Body.Systems;

// Retain Goob 5327c414c5 digestion while using the Wizards solution API.
public sealed partial class StomachSystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StomachComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StomachComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<StomachComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<StomachComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
    }

    private void OnMapInit(Entity<StomachComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _gameTiming.CurTime + ent.Comp.AdjustedUpdateInterval;
    }

    private void OnUnpaused(Entity<StomachComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.NextUpdate += args.PausedTime;
    }

    private void OnEntRemoved(Entity<StomachComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.Solution is not { } solution || args.Entity != solution.Owner)
            return;

        ent.Comp.Solution = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<StomachComponent, OrganComponent, SolutionManagerComponent>();
        while (query.MoveNext(out var uid, out var stomach, out var organ, out var solutions))
        {
            if (_gameTiming.CurTime < stomach.NextUpdate)
                continue;

            stomach.NextUpdate += stomach.AdjustedUpdateInterval;
            if (!_solutionContainerSystem.ResolveSolution((uid, solutions), DefaultSolutionName,
                    ref stomach.Solution, out var stomachSolution))
                continue;

            if (organ.Body is not { } body
                || !_solutionContainerSystem.TryGetSolution(body, stomach.BodySolutionName, out var bodySolution))
                continue;

            var transferSolution = new Solution();
            var queue = new RemQueue<StomachComponent.ReagentDelta>();
            foreach (var delta in stomach.ReagentDeltas)
            {
                delta.Increment(stomach.AdjustedUpdateInterval);
                if (delta.Lifetime <= stomach.DigestionDelay)
                    continue;

                if (stomachSolution.TryGetReagent(delta.ReagentQuantity.Reagent, out var reagent))
                {
                    if (reagent.Quantity > delta.ReagentQuantity.Quantity)
                        reagent = new(reagent.Reagent, delta.ReagentQuantity.Quantity);

                    stomachSolution.RemoveReagent(reagent);
                    transferSolution.AddReagent(reagent);
                }

                queue.Add(delta);
            }

            foreach (var item in queue)
            {
                stomach.ReagentDeltas.Remove(item);
            }

            _solutionContainerSystem.UpdateChemicals(stomach.Solution.Value);
            _solutionContainerSystem.TryAddSolution(bodySolution.Value, transferSolution);
        }
    }

    private void OnApplyMetabolicMultiplier(Entity<StomachComponent> ent, ref ApplyMetabolicMultiplierEvent args)
    {
        ent.Comp.UpdateIntervalMultiplier = args.Multiplier;
    }
}

// Retained from Goob 5327c414c5: EntityConditions/Conditions/MetabolizerTypesEntityConditionSystem.cs.
using Content.Server.Body.Components;
using Content.Shared.EntityConditions;
using Content.Shared.EntityConditions.Conditions.Body;

namespace Content.Server.EntityConditions.Conditions;

/// <summary>Checks the retained Goob organ's metabolism types for reagent effects.</summary>
public sealed partial class MetabolizerTypeEntityConditionSystem : EntityConditionSystem<MetabolizerComponent, MetabolizerTypeCondition>
{
    protected override void Condition(Entity<MetabolizerComponent> entity, ref EntityConditionEvent<MetabolizerTypeCondition> args)
    {
        if (entity.Comp.MetabolizerTypes == null)
            return;

        args.Result = entity.Comp.MetabolizerTypes.Overlaps(args.Condition.Type);
    }
}

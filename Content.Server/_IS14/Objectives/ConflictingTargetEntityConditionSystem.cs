using Content.Shared._IS14.Objectives;
using Content.Shared.EntityConditions;
using Content.Shared.Mind;

namespace Content.Server._IS14.Objectives;

public sealed partial class ConflictingTargetEntityConditionSystem : EntityConditionSystem<MindComponent, ConflictingTargetCondition>
{
    protected override void Condition(Entity<MindComponent> entity, ref EntityConditionEvent<ConflictingTargetCondition> args)
    {
        args.Result = !ConflictingTargetMindFilter.HasConflictingTarget(entity, args.SourceEnt, EntityManager);
    }
}

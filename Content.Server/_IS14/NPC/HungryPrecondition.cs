using Content.Server.NPC;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server.NPC.HTN.Preconditions;

/// <summary>Preserves Goob AI hunger thresholds through the migrated satiation data.</summary>
public sealed partial class HungryPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entities = default!;

    [DataField(required: true)]
    public HungerThreshold MinHungerState = HungerThreshold.Starving;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entities)
               && _entities.HasComponent<SatiationComponent>(owner)
               && _entities.System<HungerSystem>().GetHungerThreshold(owner) <= MinHungerState;
    }
}

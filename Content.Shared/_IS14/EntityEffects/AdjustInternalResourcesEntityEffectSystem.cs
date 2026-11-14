using Content.Goobstation.Shared.InternalResources.Components;
using Content.Goobstation.Shared.InternalResources.Data;
using Content.Goobstation.Shared.InternalResources.EntitySystems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.EntityEffects;

/// <summary>Applies reagent effects to Goob resources such as changeling chemicals.</summary>
public sealed partial class AdjustInternalResourcesEntityEffectSystem : EntityEffectSystem<InternalResourcesComponent, AdjustInternalResources>
{
    [Dependency] private SharedInternalResourcesSystem _resources = default!;

    protected override void Effect(Entity<InternalResourcesComponent> entity, ref EntityEffectEvent<AdjustInternalResources> args)
    {
        _resources.TryUpdateResourcesAmount(entity.Owner, args.Effect.Resource.Id, args.Effect.Amount * args.Scale, entity.Comp);
    }
}

public sealed partial class AdjustInternalResources : EntityEffectBase<AdjustInternalResources>
{
    [DataField(required: true)] public ProtoId<InternalResourcesPrototype> Resource;
    [DataField(required: true)] public float Amount;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-adjust-changeling-chemicals", ("amount", Amount), ("chance", Probability));
}

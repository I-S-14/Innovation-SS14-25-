using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Objectives;

/// <summary>Allows assistance targets only when their existing kill objectives do not conflict.</summary>
public sealed partial class ConflictingTargetCondition : EntityConditionBase<ConflictingTargetCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

// Retained from Goob snapshot 5327c414c5, Content.Server/Mind/Filters/TargetObjectiveMindFilter.cs.
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Filters;
using Content.Shared.Whitelist;

namespace Content.Server.Mind.Filters;

/// <summary>Exclude candidates already targeted by a matching objective of the assigning mind.</summary>
public sealed partial class TargetObjectiveMindFilter : MindFilter
{
    [DataField]
    public EntityWhitelist? Blacklist;

    protected override bool ShouldRemove(Entity<MindComponent> mind, EntityUid? excluded,
        IEntityManager entMan, SharedMindSystem mindSys)
    {
        if (!entMan.TryGetComponent<MindComponent>(excluded, out var excludedMind))
            return false;

        var whitelist = entMan.System<EntityWhitelistSystem>();
        foreach (var objective in excludedMind.Objectives)
        {
            if (entMan.TryGetComponent<TargetObjectiveComponent>(objective, out var target)
                && target.Target == mind.Owner
                && whitelist.IsWhitelistPassOrNull(Blacklist, objective))
                return true;
        }
        return false;
    }
}

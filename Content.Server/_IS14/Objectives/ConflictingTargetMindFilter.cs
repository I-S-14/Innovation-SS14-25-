using Content.Shared.Mind.Filters;
using Content.Server.Objectives.Components;
using Content.Shared.Mind;


namespace Content.Server._IS14.Objectives;

public sealed partial class ConflictingTargetMindFilter : MindFilter
{
    protected override bool ShouldRemove(Entity<MindComponent> mind, EntityUid? exclude, IEntityManager entMan, SharedMindSystem mindSys)
        => HasConflictingTarget(mind, exclude, entMan);

    // IS14: share the Goob exclusion rule with Wizards entity conditions.
    public static bool HasConflictingTarget(Entity<MindComponent> mind, EntityUid? exclude, IEntityManager entMan)
    {
        if (exclude is not { } picker || !entMan.TryGetComponent<MindComponent>(picker, out var pickerMind))
            return false;

        foreach (var objective in pickerMind.Objectives)
        {
            if (!entMan.TryGetComponent<TargetObjectiveComponent>(objective, out var target) || target.Target is not { } other)
                continue;

            if (HasKillTarget(other, mind.Owner, entMan) || HasKillTarget(mind.Owner, other, entMan))
                return true;
        }

        return false;
    }

    private static bool HasKillTarget(EntityUid killer, EntityUid victim, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<MindComponent>(killer, out var killerMind))
            return false;

        foreach (var objective in killerMind.Objectives)
        {
            if (entMan.HasComponent<KillPersonConditionComponent>(objective)
                && entMan.TryGetComponent<TargetObjectiveComponent>(objective, out var target)
                && target.Target == victim)
                return true;
        }

        return false;
    }
}

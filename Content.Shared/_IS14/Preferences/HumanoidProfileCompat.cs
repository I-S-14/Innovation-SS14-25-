using Content.Shared.CCVar;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public HumanoidCharacterProfile WithJobFromCvar(IConfigurationManager cfg)
    {
        var priority = JobPriority.High;
        var jobs = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
        foreach (var job in cfg.GetCVar(CCVars.NewCharacterJobs).Split(','))
        {
            var id = job.Trim();
            if (id.Length == 0 || jobs.ContainsKey(id))
                continue;
            jobs.Add(id, priority);
            priority = JobPriority.Medium;
        }
        return WithJobPriorities(jobs);
    }
}

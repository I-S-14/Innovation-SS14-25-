using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>Fallback marker job on retained maps when no exact job marker exists.</summary>
    [DataField]
    public ProtoId<JobPrototype>? SpawnPointJob;
}

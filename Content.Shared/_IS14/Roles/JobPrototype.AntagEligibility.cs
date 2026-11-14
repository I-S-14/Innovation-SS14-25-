namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>Preserved Goob/IS14 restriction on antagonists for this job.</summary>
    [DataField]
    public bool CanBeAntag { get; private set; } = true;
}

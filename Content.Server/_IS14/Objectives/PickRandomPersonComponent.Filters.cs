using Content.Shared.Mind.Filters;

namespace Content.Server.Objectives.Components;

public sealed partial class PickRandomPersonComponent
{
    /// <summary>Retained Goob exclusions, applied in addition to Wizards conditions.</summary>
    [DataField]
    public List<MindFilter> Filters = new();
}

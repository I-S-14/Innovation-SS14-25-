using Robust.Shared.Utility;

namespace Content.Shared.Actions.Components;

public sealed partial class ActionComponent
{
    /// <summary>Legacy Goob icon, projected into the new action appearance on initialization.</summary>
    [DataField]
    public SpriteSpecifier? Icon;
}

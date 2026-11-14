using Robust.Shared.GameStates;

namespace Content.Shared.Nutrition.Components;

public sealed partial class ButcherableComponent
{
    /// <summary>Retained Goob/IS14 distinction between knives and meat spikes.</summary>
    [DataField("butcheringType"), AutoNetworkedField]
    public ButcheringType Type = ButcheringType.Knife;
}

public enum ButcheringType : byte
{
    Knife,
    Spike,
    Gibber,
}

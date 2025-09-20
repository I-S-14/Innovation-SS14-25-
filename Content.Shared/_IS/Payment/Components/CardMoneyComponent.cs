using Robust.Shared.GameStates;

namespace Content.Shared._IS.Payment.Components;

/// <summary>
/// This is used for card holochips / etc card balance sources
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardMoneyComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Value { get; set; }
}

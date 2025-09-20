using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS.Payment.Components;

/// <summary>
/// This is used for ID card balance feature
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardBalanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Balance;

    [DataField, AutoNetworkedField]
    public string PersonalAccount = "";

    // Any entity prototype having CardMoneyComponent
    [DataField]
    public EntProtoId<CardMoneyComponent> WithdrawPrototype = "Holochip";
}

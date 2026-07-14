using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.Atm;

[RegisterComponent]
public sealed partial class IS14AtmComponent : Component
{
    /// <summary>ItemSlots slot ID the ID card is inserted into.</summary>
    [DataField]
    public string CardSlotId = "is14-atm-card-slot";

    /// <summary>Stack prototype spawned on withdrawal and accepted on deposit.</summary>
    [DataField]
    public ProtoId<StackPrototype> CashStackType = "Credit";

    /// <summary>Failed PIN entries before the account is locked and the card is ejected.</summary>
    [DataField]
    public int MaxPinAttempts = 3;

    /// <summary>How long the account stays locked after too many failed PIN entries.</summary>
    [DataField]
    public TimeSpan LockDuration = TimeSpan.FromMinutes(2);

    [DataField]
    public SoundSpecifier AcceptSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    /// <summary>Whether the current session has passed PIN entry. Reset when the card leaves or the UI closes.</summary>
    [ViewVariables]
    public bool Authenticated;
}

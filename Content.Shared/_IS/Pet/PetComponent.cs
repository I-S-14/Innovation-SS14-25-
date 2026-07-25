using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS.Pet.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PetComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Overlord;

    [DataField]
    public List<ProtoId<PetOrderPrototype>> AvailableOrders = new();
    [DataField, AutoNetworkedField]
    public PetOrderType CurrentOrder;
}

[RegisterComponent]
public sealed partial class PetCreatorComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PetOverlordComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Pets = new HashSet<EntityUid>();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public PetOrderType CurrentOrder = PetOrderType.Follow;

    [DataField]
    public EntProtoId StayAction = "ActionPetStay";

    [DataField, AutoNetworkedField]
    public EntityUid? StayActionEntity;

    [DataField]
    public EntProtoId AttackAction = "ActionPetAttack";

    [DataField, AutoNetworkedField]
    public EntityUid? AttackActionEntity;

    [DataField]
    public EntProtoId FollowAction = "ActionPetFollow";

    [DataField, AutoNetworkedField]
    public EntityUid? FollowActionEntity;
}

[RegisterComponent]
public sealed partial class PetMakeOverlordOnOpenComponent : Component
{
    [DataField]
    public EntProtoId PetToSpawn = "HoSDog";
}

[Serializable, NetSerializable]
public enum PetOrderType : byte
{
    Stay,
    Follow,
    Attack
}

public sealed partial class PetOrderActionEvent : InstantActionEvent
{
    [DataField("type")]
    public PetOrderType Type;
}

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS.CardSticker;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CardStickerTargetComponent : Component
{
    /// <summary>
    /// The current card sticker prototype being displayed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CardStickerPrototype>? Current;

    // TODO: Add specified layer?
}

[Serializable, NetSerializable]
public sealed class SetCardStickerMessage(ProtoId<CardStickerPrototype> sticker) : EntityEventArgs
{
    public ProtoId<CardStickerPrototype> Sticker = sticker;
}

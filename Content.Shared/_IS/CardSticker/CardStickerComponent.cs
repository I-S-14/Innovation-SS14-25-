using Robust.Shared.Prototypes;

namespace Content.Shared._IS.CardSticker;

/// <summary>
/// This is used for sticker data holding.
/// </summary>
[RegisterComponent]
public sealed partial class CardStickerComponent : Component
{
    [DataField(readOnly: true, required: true)]
    public ProtoId<CardStickerPrototype> CardStickerProto;
}

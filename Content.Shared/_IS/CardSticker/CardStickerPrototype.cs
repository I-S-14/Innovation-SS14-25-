using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS.CardSticker;

[Prototype("cardSticker")]
public sealed partial class CardStickerPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField] public LocId Name { get; private set; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon { get; private set; } = default!;
}

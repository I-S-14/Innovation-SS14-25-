using Content.Shared._IS.CardSticker;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._IS.CardSticker;

public sealed class CardStickerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardStickerTargetComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<CardStickerTargetComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateAppearance(ent.Owner, ent.Comp);
    }

    private void UpdateAppearance(EntityUid id,
        CardStickerTargetComponent sticker,
        SpriteComponent? sprite = null)
    {
        if (!Resolve(id, ref sprite))
            return;

        if (!_protoMan.TryIndex(sticker.Current, out var stickerPrototype))
            return;

        _sprite.LayerSetSprite((id, sprite), IdCardVisualLayers.Sticker, stickerPrototype.Icon);
    }
}

using Robust.Client.GameObjects;
using Content.Shared._IS.CardSticker;

namespace Content._IS.IdentificationCard;

public sealed class IdCardVisualizerSystem : VisualizerSystem<IdCardVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    protected override void OnAppearanceChange(EntityUid uid, IdCardVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<string>(uid, IdCardVisualLayers.Base, out var idType, args.Component))
            _sprite.LayerSetRsiState((uid, args.Sprite), IdCardVisualLayers.Base, idType);
    }
}

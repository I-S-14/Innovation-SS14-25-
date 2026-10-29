// SPDX-License-Identifier: MIT

using Content.Client.Storage.Components;
using Content.Shared.Rounding;
using Content.Shared.Storage;
using Robust.Client.GameObjects;

namespace Content.Client.Storage.Systems;

/// <inheritdoc cref="StorageContainerVisualsComponent"/>
public sealed partial class StorageContainerVisualsSystem : VisualizerSystem<StorageContainerVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, StorageContainerVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.TryGetData<int>(StorageVisuals.StorageUsed, out var used))
            return;

        if (!args.TryGetData<int>(StorageVisuals.Capacity, out var capacity))
            return;

        var fraction = used / (float)capacity;

        if (!_sprite.LayerMapTryGet((uid, args.Sprite), component.FillLayer, out var fillLayer, false))
            return;

        var closestFillSprite = Math.Min(ContentHelpers.RoundToNearestLevels(fraction, 1, component.MaxFillLevels + 1),
            component.MaxFillLevels);

        if (closestFillSprite > 0)
        {
            if (component.FillBaseName == null)
                return;

            _sprite.LayerSetVisible((uid, args.Sprite), fillLayer, true);
            var stateName = component.FillBaseName + closestFillSprite;
            _sprite.LayerSetRsiState((uid, args.Sprite), fillLayer, stateName);
        }
        else
        {
            _sprite.LayerSetVisible((uid, args.Sprite), fillLayer, false);
        }
    }
}
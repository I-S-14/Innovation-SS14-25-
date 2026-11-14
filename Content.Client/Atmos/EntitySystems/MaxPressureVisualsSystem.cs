using Content.Client.Atmos.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;

namespace Content.Client.Atmos.EntitySystems;

/// <summary>
/// This system handles sprite changes for a <see cref="IGasMaxPressureHolder"/>
/// with a <see cref="MaxPressureVisualsComponent"/> when its <see cref="IGasMaxPressureHolder.Integrity"/> changes.
/// </summary>
public sealed partial class MaxPressureVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    [SubscribeLocalEvent]
    private void OnMaxPressureInit(Entity<MaxPressureVisualsComponent> entity, ref ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        //IS14-change: retain custom tank artwork without requiring foreign masks.
        if (entity.Comp.IntegrityColor != null)
        {
            entity.Comp.OriginalColor = sprite.Color;
            return;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entity.Comp.IntegritySteps);

        if (_sprite.LayerMapTryGet((entity, sprite), MaxPressureVisualLayers.Base, out var layerIndex, false))
        {
            _sprite.LayerSetRsiState((entity, sprite), layerIndex, $"{entity.Comp.IntegrityMask}");
            _sprite.LayerSetVisible((entity, sprite), layerIndex, false);
        }

        if (_sprite.LayerMapTryGet((entity, sprite), MaxPressureVisualLayers.BaseUnshaded, out layerIndex, false))
        {
            _sprite.LayerSetRsiState((entity, sprite), layerIndex, $"{entity.Comp.IntegrityState}-unshaded-0");
            _sprite.LayerSetVisible((entity, sprite), layerIndex, false);
        }
    }

    [SubscribeLocalEvent]
    private void OnAppearanceChange(Entity<MaxPressureVisualsComponent> entity, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!args.TryGetData<float>(GasIntegrity.Integrity, out var integrity)
            || !args.TryGetData<float>(GasIntegrity.MaxIntegrity, out var maxIntegrity))
            return;

        //IS14-change: old tank RSIs show damage by tint and restore their original colour on recovery.
        if (entity.Comp.IntegrityColor is { } warningColor)
        {
            var loss = maxIntegrity > 0 ? Math.Clamp((maxIntegrity - integrity) / maxIntegrity, 0f, 1f) : 0f;
            var alpha = sprite.Color.A;
            _sprite.SetColor((entity, sprite), Color.InterpolateBetween(entity.Comp.OriginalColor.WithAlpha(alpha),
                warningColor.WithAlpha(alpha), loss * 0.75f));
            return;
        }

        // We don't want visuals at max integrity, so we return if we're at max.
        if (integrity >= maxIntegrity)
        {
            _sprite.LayerSetVisible((entity, sprite), MaxPressureVisualLayers.Base, false);
            _sprite.LayerSetVisible((entity, sprite), MaxPressureVisualLayers.BaseUnshaded, false);
            return;
        }

        _sprite.LayerSetVisible((entity, sprite), MaxPressureVisualLayers.Base, true);
        _sprite.LayerSetVisible((entity, sprite), MaxPressureVisualLayers.BaseUnshaded, true);

        // Subtract our integrity + 1 to get an accurate step count.
        if (entity.Comp.IntegritySteps > 1)
        {
            var step = ContentHelpers.RoundToEqualLevels(maxIntegrity - integrity - 1, maxIntegrity, entity.Comp.IntegritySteps);
            _sprite.LayerSetRsiState((entity, sprite), MaxPressureVisualLayers.BaseUnshaded, $"{entity.Comp.IntegrityState}-unshaded-{step}");
        }
        else
        {
            _sprite.LayerSetRsiState((entity, sprite), MaxPressureVisualLayers.BaseUnshaded, $"{entity.Comp.IntegrityState}-unshaded-0");
        }
    }
}

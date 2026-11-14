// SPDX-License-Identifier: MIT

using Content.Shared.Power;
using Content.Shared.Power.Components;
using Robust.Client.GameObjects;

namespace Content.Client.PowerCell;

public sealed partial class PowerChargerVisualizerSystem : VisualizerSystem<PowerChargerVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, PowerChargerVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Update base item
        if (args.TryGetData<bool>(CellVisual.Occupied, out var occupied) && occupied)
        {
            // TODO: don't throw if it doesn't have a full state
            _sprite.LayerSetRsiState((uid, args.Sprite), PowerChargerVisualLayers.Base, comp.OccupiedState);
        }
        else
        {
            _sprite.LayerSetRsiState((uid, args.Sprite), PowerChargerVisualLayers.Base, comp.EmptyState);
        }

        // Update lighting
        if (args.TryGetData<CellChargerStatus>(CellVisual.Light, out var status)
            && comp.LightStates.TryGetValue(status, out var lightState))
        {
            _sprite.LayerSetRsiState((uid, args.Sprite), PowerChargerVisualLayers.Light, lightState);
            _sprite.LayerSetVisible((uid, args.Sprite), PowerChargerVisualLayers.Light, true);
        }
        else
            _sprite.LayerSetVisible((uid, args.Sprite), PowerChargerVisualLayers.Light, false);
    }
}

public enum PowerChargerVisualLayers : byte
{
    Base,
    Light,
    ItemDisplay, // WWDP
}

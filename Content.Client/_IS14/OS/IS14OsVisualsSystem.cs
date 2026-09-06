using Content.Shared._IS14.OS.Components;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.OS;

/// <summary>
///     Lid state on the sprite. The art already ships "closed" and "base" (open) states plus an
///     unshaded screen overlay, so an open PDA is visible to everyone around you.
/// </summary>
public sealed class IS14OsVisualsSystem : VisualizerSystem<IS14OsDeviceComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, IS14OsDeviceComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = new Entity<SpriteComponent?>(uid, args.Sprite);

        if (!AppearanceSystem.TryGetData<bool>(uid, IS14OsVisuals.LidOpen, out var lidOpen, args.Component))
            lidOpen = false;

        if (!AppearanceSystem.TryGetData<bool>(uid, IS14OsVisuals.ScreenOn, out var screenOn, args.Component))
            screenOn = false;

        if (SpriteSystem.LayerMapTryGet(sprite, IS14OsVisualLayers.Base, out var baseLayer, false))
            SpriteSystem.LayerSetRsiState(sprite, baseLayer, lidOpen ? comp.OpenState : comp.ClosedState);

        if (SpriteSystem.LayerMapTryGet(sprite, IS14OsVisualLayers.Screen, out var screenLayer, false))
            SpriteSystem.LayerSetVisible(sprite, screenLayer, screenOn);
    }
}

using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.VendingMachines;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Economy.VendingMachine;

/// <summary>
/// Applies vanilla-style vending machine visuals to IS14 machines:
/// unshaded screen while powered, deny flash, broken base state, dark when unpowered.
/// </summary>
public sealed class IS14VendingMachineVisualsSystem : VisualizerSystem<IS14VendingMachineComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, IS14VendingMachineComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(IS14VendingMachineVisuals.VisualState, out var stateObject) ||
            stateObject is not IS14VendingMachineVisualState state)
        {
            state = IS14VendingMachineVisualState.Normal;
        }

        var sprite = new Entity<SpriteComponent?>(uid, args.Sprite);

        // Base layer: broken chassis or the regular unlit chassis.
        if (SpriteSystem.LayerMapTryGet(sprite, VendingMachineVisualLayers.Base, out var baseLayer, false))
        {
            SpriteSystem.LayerSetRsiState(sprite, baseLayer,
                state == IS14VendingMachineVisualState.Broken ? comp.BrokenState : comp.OffState);
        }

        // Unshaded screen layer: lit only while powered and unbroken.
        if (SpriteSystem.LayerMapTryGet(sprite, VendingMachineVisualLayers.BaseUnshaded, out var screenLayer, false))
        {
            var screenVisible = state is IS14VendingMachineVisualState.Normal or IS14VendingMachineVisualState.Deny;
            SpriteSystem.LayerSetVisible(sprite, screenLayer, screenVisible);

            if (screenVisible)
            {
                SpriteSystem.LayerSetRsiState(sprite, screenLayer,
                    state == IS14VendingMachineVisualState.Deny ? comp.DenyState : comp.NormalState);
            }
        }
    }
}

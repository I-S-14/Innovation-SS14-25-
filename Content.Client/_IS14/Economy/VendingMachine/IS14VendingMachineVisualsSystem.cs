using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.VendingMachines;
using Content.Client.Light.Components;
using Content.Client.Light.EntitySystems;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Economy.VendingMachine;

/// <summary>
/// Applies vanilla-style vending machine visuals to IS14 machines:
/// unshaded screen while powered, deny flash, broken base state, dark when unpowered.
/// </summary>
public sealed class IS14VendingMachineVisualsSystem : VisualizerSystem<IS14VendingMachineComponent>
{
    [Dependency] private readonly LightBehaviorSystem _lightBehaviors = default!;
    [Dependency] private readonly PointLightSystem _lights = default!;

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
        var screenVisible = state is IS14VendingMachineVisualState.Normal or IS14VendingMachineVisualState.Deny or IS14VendingMachineVisualState.Eject;

        // Light behaviours enable their light on every loop. Stop the behaviour
        // before disabling the light so unpowered or broken vendors stay dark.
        if (TryComp<LightBehaviourComponent>(uid, out var behaviour))
        {
            if (screenVisible)
                _lightBehaviors.StartLightBehaviour((uid, behaviour), "screen-flicker");
            else
                _lightBehaviors.StopLightBehaviour((uid, behaviour), "screen-flicker");
        }

        if (TryComp<PointLightComponent>(uid, out var light))
            _lights.SetEnabled(uid, screenVisible, light);

        // Base layer: broken chassis or the regular unlit chassis.
        if (SpriteSystem.LayerMapTryGet(sprite, VendingMachineVisualLayers.Base, out var baseLayer, false))
        {
            SpriteSystem.LayerSetRsiState(sprite, baseLayer,
                state == IS14VendingMachineVisualState.Broken ? comp.BrokenState : comp.OffState);
        }

        // Unshaded screen layer: lit only while powered and unbroken.
        if (SpriteSystem.LayerMapTryGet(sprite, VendingMachineVisualLayers.BaseUnshaded, out var screenLayer, false))
        {
            SpriteSystem.LayerSetVisible(sprite, screenLayer, screenVisible);

            if (screenVisible)
            {
                var screenState = state switch
                {
                    IS14VendingMachineVisualState.Deny => comp.DenyState,
                    IS14VendingMachineVisualState.Eject => comp.EjectState ?? comp.NormalState,
                    _ => comp.NormalState,
                };
                SpriteSystem.LayerSetRsiState(sprite, screenLayer, screenState);
                if (state == IS14VendingMachineVisualState.Eject &&
                    args.TryGetData<uint>(IS14VendingMachineVisuals.EjectSequence, out var sequence) &&
                    sequence != comp.LastEjectSequence)
                {
                    SpriteSystem.LayerSetAnimationTime(sprite, screenLayer, 0f);
                    comp.LastEjectSequence = sequence;
                }
            }
        }
    }
}

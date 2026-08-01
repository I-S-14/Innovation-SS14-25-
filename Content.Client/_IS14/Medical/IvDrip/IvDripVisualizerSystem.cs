// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.IvDrip;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Medical.IvDrip;

/// <summary>
/// Paints a drip stand. Every decision behind what it draws was already made on the
/// server — the pose is picked, the fill is bucketed, the colour is mixed — so this only
/// looks state names up and toggles two layers.
/// </summary>
public sealed class IvDripVisualizerSystem : VisualizerSystem<IvDripVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(
        EntityUid uid,
        IvDripVisualsComponent comp,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);

        var state = AppearanceSystem.TryGetData<IvDripVisualState>(uid, IvDripVisuals.State, out var pose, args.Component)
            ? pose
            : IvDripVisualState.Idle;

        if (comp.States.TryGetValue(state, out var stateName))
            _sprite.LayerSetRsiState(sprite, IvDripVisualLayers.Base, stateName);

        var hasPack = AppearanceSystem.TryGetData<bool>(uid, IvDripVisuals.HasPack, out var pack, args.Component)
                      && pack;

        _sprite.LayerSetVisible(sprite, IvDripVisualLayers.Beaker, hasPack);
        _sprite.LayerSetVisible(sprite, IvDripVisualLayers.Reagent, hasPack);

        if (!hasPack)
            return;

        // The bag only animates while something is going through it, which is the same
        // condition the pole runs on.
        var running = state is IvDripVisualState.Injecting or IvDripVisualState.Drawing;

        _sprite.LayerSetRsiState(
            sprite,
            IvDripVisualLayers.Beaker,
            running ? comp.BeakerActiveState : comp.BeakerIdleState);

        var level = AppearanceSystem.TryGetData<int>(uid, IvDripVisuals.FillLevel, out var fill, args.Component)
            ? Math.Clamp(fill, 0, comp.ReagentStates.Count - 1)
            : 0;

        if (comp.ReagentStates.Count > 0)
            _sprite.LayerSetRsiState(sprite, IvDripVisualLayers.Reagent, comp.ReagentStates[level]);

        if (AppearanceSystem.TryGetData<Color>(uid, IvDripVisuals.FillColor, out var color, args.Component))
            _sprite.LayerSetColor(sprite, IvDripVisualLayers.Reagent, color);
    }
}

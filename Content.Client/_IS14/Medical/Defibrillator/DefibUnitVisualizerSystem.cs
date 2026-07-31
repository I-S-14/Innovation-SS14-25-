// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.Defibrillator;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Medical.Defibrillator;

/// <summary>
/// Paints the unit's face. Everything it draws is a fact the server already decided —
/// the charge arrives pre-bucketed — so this only picks states and toggles layers.
/// </summary>
public sealed class DefibUnitVisualizerSystem : VisualizerSystem<DefibUnitVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(
        EntityUid uid,
        DefibUnitVisualsComponent comp,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);

        var hasCell = AppearanceSystem.TryGetData<bool>(uid, DefibUnitVisuals.HasCell, out var cell, args.Component)
                      && cell;

        // The plate over the empty slot is the loudest thing on the box, and it should
        // be: a unit with no cell is the one failure a doctor can fix on the spot.
        _sprite.LayerSetVisible(sprite, DefibUnitVisualLayers.NoCell, !hasCell);

        var level = AppearanceSystem.TryGetData<int>(uid, DefibUnitVisuals.Charge, out var charge, args.Component)
            ? charge
            : 0;

        // Level zero has no state of its own — an empty gauge is simply not drawn.
        if (hasCell && level > 0 && comp.ChargeStates.Count >= level)
        {
            _sprite.LayerSetRsiState(sprite, DefibUnitVisualLayers.Charge, comp.ChargeStates[level - 1]);
            _sprite.LayerSetVisible(sprite, DefibUnitVisualLayers.Charge, true);
        }
        else
        {
            _sprite.LayerSetVisible(sprite, DefibUnitVisualLayers.Charge, false);
        }

        var docked = AppearanceSystem.TryGetData<bool>(uid, DefibUnitVisuals.PaddlesDocked, out var paddles, args.Component)
                     && paddles;

        _sprite.LayerSetVisible(sprite, DefibUnitVisualLayers.Paddles, docked);

        var active = AppearanceSystem.TryGetData<bool>(uid, DefibUnitVisuals.Active, out var powered, args.Component)
                     && powered;

        _sprite.LayerSetVisible(sprite, DefibUnitVisualLayers.Powered, active);

        var emagged = AppearanceSystem.TryGetData<bool>(uid, DefibUnitVisuals.Emagged, out var hacked, args.Component)
                      && hacked;

        _sprite.LayerSetVisible(sprite, DefibUnitVisualLayers.Emagged, emagged);
    }
}

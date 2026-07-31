// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Containers;
using Content.Shared._IS14.Medical.Defibrillator;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Medical.Defibrillator;

/// <summary>
/// Paints a wall station or crash cart. Everything but the frame belongs to the unit
/// racked in it, and arrives on the rack's own appearance through the generic contained
/// appearance relay — so this reads the unit's keys off the rack and decides how they
/// look on the rack's sheet.
/// </summary>
public sealed class DefibMountVisualizerSystem : VisualizerSystem<DefibMountVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(
        EntityUid uid,
        DefibMountVisualsComponent comp,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);

        var hasUnit = AppearanceSystem.TryGetData<bool>(uid, ContainedRelayVisuals.HasContents, out var unit, args.Component)
                      && unit;

        _sprite.LayerSetVisible(sprite, DefibMountVisualLayers.Defib, hasUnit);

        var level = AppearanceSystem.TryGetData<int>(uid, DefibUnitVisuals.Charge, out var charge, args.Component)
            ? charge
            : 0;

        if (hasUnit && level > 0 && comp.ChargeStates.Count >= level)
        {
            _sprite.LayerSetRsiState(sprite, DefibMountVisualLayers.Charge, comp.ChargeStates[level - 1]);
            _sprite.LayerSetVisible(sprite, DefibMountVisualLayers.Charge, true);
        }
        else
        {
            _sprite.LayerSetVisible(sprite, DefibMountVisualLayers.Charge, false);
        }

        // Online means there is a cell with something left in it. A rack reading ready
        // over a flat unit would send somebody running with a brick.
        var hasCell = AppearanceSystem.TryGetData<bool>(uid, DefibUnitVisuals.HasCell, out var cell, args.Component)
                      && cell;

        _sprite.LayerSetVisible(sprite, DefibMountVisualLayers.Online, hasUnit && hasCell && level > 0);

        var emagged = AppearanceSystem.TryGetData<bool>(uid, DefibUnitVisuals.Emagged, out var hacked, args.Component)
                      && hacked;

        _sprite.LayerSetVisible(sprite, DefibMountVisualLayers.Emagged, hasUnit && emagged);
    }
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Atmos.EntitySystems;
using Content.Client.SubFloor;
using Content.Shared._IS14.Ordnance;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Ordnance;

/// <summary>
/// Draws a loaded manual valve as the transfer valve it has become: the pipework and its connection
/// stubs disappear, the assembly's own icon takes over, and the trigger's cabling goes on top.
/// </summary>
/// <remarks>
/// Applied both when the appearance changes and on every frame, because three other systems write
/// to these same layers — <c>SubFloorHideSystem</c> makes every layer visible again,
/// <c>AtmosPipeAppearanceSystem</c> owns the connection stubs, and <c>AtmosPipeLayersSystem</c>
/// reassigns the sprite's RSI — and the order between them and us is not entirely ours to decide.
/// The frame update costs a query over the station's pipe valves and has the last word whatever
/// any of them did.
/// </remarks>
public sealed class IS14TransferValveVisualsSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <summary>The stubs <c>AtmosPipeAppearanceSystem</c> draws towards neighbouring pipes.</summary>
    private static readonly string[] Connections =
        { "NorthConnection", "SouthConnection", "EastConnection", "WestConnection" };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14TransferValveComponent, ComponentStartup>((uid, _, _) =>
            EnsureComp<IS14TransferValveVisualsComponent>(uid));

        SubscribeLocalEvent<IS14TransferValveComponent, AppearanceChangeEvent>(OnAppearanceChange,
            after: new[] { typeof(SubFloorHideSystem), typeof(AtmosPipeAppearanceSystem), typeof(AtmosPipeLayersSystem) });
    }

    private void OnAppearanceChange(Entity<IS14TransferValveComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        Update(ent.Owner, EnsureComp<IS14TransferValveVisualsComponent>(ent.Owner), args.Component, args.Sprite);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<IS14TransferValveVisualsComponent, AppearanceComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var tracked, out var appearance, out var spriteComp))
        {
            Update(uid, tracked, appearance, spriteComp);
        }
    }

    private void Update(EntityUid uid,
        IS14TransferValveVisualsComponent tracked,
        AppearanceComponent appearance,
        SpriteComponent spriteComp)
    {
        _appearance.TryGetData<int>(uid, IS14TransferValveVisuals.Tanks, out var tanks, appearance);
        _appearance.TryGetData<bool>(uid, IS14TransferValveVisuals.Trigger, out var trigger, appearance);

        var assembled = tanks > 0;
        var sprite = (uid, spriteComp);

        if (!assembled)
        {
            // Nothing on it: hand the sprite back to the systems that own a pipe valve, and only on
            // the frame it stops being an assembly, so we are not fighting them for the rest.
            if (!tracked.Assembled)
                return;

            tracked.Assembled = false;

            SetVisible(sprite, PipeVisualLayers.Pipe, true);
            SetVisible(sprite, SubfloorLayers.FirstLayer, true);
            SetVisible(sprite, IS14TransferValveVisualLayers.Assembly, false);
            SetVisible(sprite, IS14TransferValveVisualLayers.Trigger, false);

            // Under a floor it should go back to being invisible, and that is the subfloor system's
            // call, not ours.
            _appearance.QueueUpdate(uid, appearance);
            return;
        }

        tracked.Assembled = true;

        SetVisible(sprite, PipeVisualLayers.Pipe, false);
        SetVisible(sprite, SubfloorLayers.FirstLayer, false);

        foreach (var connection in Connections)
        {
            for (var i = 0; i < 3; i++)
            {
                if (_sprite.LayerMapTryGet(sprite, connection + i, out var stub, false))
                    _sprite.LayerSetVisible(sprite, stub, false);
            }
        }

        if (_sprite.LayerMapTryGet(sprite, IS14TransferValveVisualLayers.Assembly, out var assembly, false))
        {
            _sprite.LayerSetVisible(sprite, assembly, true);
            _sprite.LayerSetRsiState(sprite, assembly, tanks >= 2 ? "valve_1" : "valve");
        }

        SetVisible(sprite, IS14TransferValveVisualLayers.Trigger, trigger);
    }

    private void SetVisible(Entity<SpriteComponent?> sprite, Enum key, bool value)
    {
        if (_sprite.LayerMapTryGet(sprite, key, out var index, false))
            _sprite.LayerSetVisible(sprite, index, value);
    }
}

/// <summary>Client-side bookkeeping: whether the valve is currently drawn as an assembly.</summary>
[RegisterComponent]
public sealed partial class IS14TransferValveVisualsComponent : Component
{
    public bool Assembled;
}

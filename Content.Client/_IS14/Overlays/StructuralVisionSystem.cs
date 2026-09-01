// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.Overlays;
using Content.Shared._IS14.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;

namespace Content.Client._IS14.Overlays;

/// <summary>
///     Switches <see cref="StructuralVisionOverlay"/> on while the player is wearing
///     something that grants <see cref="StructuralVisionComponent"/>.
///
///     Short because the upstream base class already owns all of the bookkeeping — equip,
///     unequip, the player being attached or detached, round restart. Adding a second
///     source of structural vision later needs no code here at all.
/// </summary>
public sealed class StructuralVisionSystem : EquipmentHudSystem<StructuralVisionComponent>
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private StructuralVisionOverlay _structural = default!;

    /// <summary>
    ///     Eyes only. It is an instrument you look through, and restricting the slot keeps
    ///     it from being granted by accident through some unrelated relayed component.
    /// </summary>
    protected override SlotFlags TargetSlots => SlotFlags.EYES;

    public override void Initialize()
    {
        base.Initialize();

        // Settings arrive over the network, and the first state can land after the item is
        // already equipped — without this the overlay would keep drawing the defaults until
        // the next equip.
        SubscribeLocalEvent<StructuralVisionComponent, AfterAutoHandleStateEvent>(OnHandleState);

        _structural = new StructuralVisionOverlay();
    }

    private void OnHandleState(Entity<StructuralVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<StructuralVisionComponent> args)
    {
        base.UpdateInternal(args);

        // Two sources at once is a corner case, but it still has to resolve to something.
        // The one that reaches furthest wins outright, colours included — blending two
        // palettes would produce a third that nobody chose.
        StructuralVisionComponent? best = null;

        foreach (var comp in args.Components)
        {
            if (best == null || comp.Range > best.Range)
                best = comp;
        }

        if (best == null)
            return;

        _structural.Settings = best;

        if (!_overlay.HasOverlay<StructuralVisionOverlay>())
            _overlay.AddOverlay(_structural);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        _overlay.RemoveOverlay(_structural);
    }
}

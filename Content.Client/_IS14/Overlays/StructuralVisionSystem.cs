// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.Overlays;
using Content.Client.SubFloor;
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

        // The scanner redraws the layout through the field of view. Anything else drawn in
        // the normal entity pass is still cut by it, which is why a t-ray shows pipes in the
        // room you are standing in and nothing next door. Listing its marker here hands those
        // entities to the same stencil pass.
        //
        // The mining scanner needs nothing: it is a world-space overlay of its own and so is
        // already drawn past the mask. It only ever looked broken because this overlay could
        // be drawn on top of it — see LayoutZIndex.
        AddSource<TrayRevealedComponent>();
    }

    /// <summary>
    ///     Also redraw entities carrying <typeparamref name="T"/> inside the scanned area.
    ///     For hooking up another scanner that marks what it has revealed with a component of
    ///     its own; the marker is expected to exist only while that scanner is running, since
    ///     nothing here checks whether the wearer is entitled to see it.
    /// </summary>
    public void AddSource<T>() where T : IComponent
    {
        if (!_structural.ExtraSources.Contains(typeof(T)))
            _structural.ExtraSources.Add(typeof(T));
    }

    public void RemoveSource<T>() where T : IComponent
    {
        _structural.ExtraSources.Remove(typeof(T));
    }

    private void OnHandleState(Entity<StructuralVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<StructuralVisionComponent> args)
    {
        base.UpdateInternal(args);

        // Two sources at once is a corner case, but it still has to resolve to something.
        // The one that reaches furthest wins.
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

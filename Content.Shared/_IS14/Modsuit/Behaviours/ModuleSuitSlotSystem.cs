// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared._IS14.Modular.Behaviours;
using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.Modsuit.Behaviours;

/// <summary>
///     Puts a module's item slot on the plating and takes it away again.
/// </summary>
public sealed class ModuleSuitSlotSystem : ModuleBehaviourSystem<ModuleSuitSlotComponent>
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedModularChassisSystem _chassis = default!;
    [Dependency] private readonly SharedModsuitSystem _modsuit = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitPartComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    /// <summary>
    ///     Keeps a slot to the kind of thing it was built around. The slot's own whitelist
    ///     can only ask what components an item has, and every garment on the station is
    ///     <c>Clothing</c> — so a cradle sized for a hat would otherwise happily swallow a
    ///     pair of boots and then draw nothing.
    /// </summary>
    private void OnInsertAttempt(Entity<ModsuitPartComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled
            || args.Slot.ContainerSlot is not { } container
            || ent.Comp.Control is not { } control
            || !TryComp<ModularChassisComponent>(control, out var chassis))
        {
            return;
        }

        foreach (var module in _chassis.GetModuleEntities((control, chassis)))
        {
            if (!TryComp<ModuleSuitSlotComponent>(module, out var suitSlot)
                || suitSlot.WearableIn == SlotFlags.NONE
                || suitSlot.Part != ent.Comp.Slot
                || suitSlot.SlotId != container.ID)
            {
                continue;
            }

            if (!TryComp<ClothingComponent>(args.Item, out var clothing)
                || (clothing.Slots & suitSlot.WearableIn) == 0)
            {
                args.Cancelled = true;
            }

            return;
        }
    }

    /// <summary>
    ///     The slot follows the module being installed, not the suit being switched on.
    ///     A holster that empties itself when the battery dies is a holster nobody would
    ///     put a gun in.
    /// </summary>
    protected override bool FollowsInstallation(Entity<ModuleSuitSlotComponent> ent) => true;

    protected override void Start(Entity<ModuleSuitSlotComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.GrantedTo != null)
            return;

        if (!TryComp<ModsuitControlComponent>(chassis, out var control)
            || !_modsuit.TryGetPart((chassis, control), ent.Comp.Part, out var part))
        {
            return;
        }

        _itemSlots.AddItemSlot(part, ent.Comp.SlotId, ent.Comp.Slot);
        ent.Comp.GrantedTo = part;
    }

    protected override void Stop(Entity<ModuleSuitSlotComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.GrantedTo is not { } part)
            return;

        ent.Comp.GrantedTo = null;

        if (TerminatingOrDeleted(part))
            return;

        // Tearing the slot down shuts its container down, and a ContainerSlot shutting
        // down deletes what is inside it. Pulling the cradle off the plating should leave
        // the hat on the floor rather than destroy it, so it comes out under its own
        // steam first.
        if (ent.Comp.Slot.ContainerSlot?.ContainedEntity is { } held
            && _container.Remove(held, ent.Comp.Slot.ContainerSlot))
        {
            _transform.DropNextTo(held, chassis);
        }

        _itemSlots.RemoveItemSlot(part, ent.Comp.Slot);
    }
}

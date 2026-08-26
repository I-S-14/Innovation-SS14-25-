// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Bridges a MOD core to the chassis power events. The chassis never learns what a
///     core is — it asks for charge and this answers, which is exactly the seam a mech
///     needs in order to answer with its own internal battery instead.
/// </summary>
public sealed class ModCoreSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModCoreSlotComponent, EntInsertedIntoContainerMessage>(OnCoreInserted);
        SubscribeLocalEvent<ModCoreSlotComponent, EntRemovedFromContainerMessage>(OnCoreRemoved);

        // The chassis raises these on itself; we relay them to whichever core is installed.
        SubscribeLocalEvent<ModCoreSlotComponent, ChassisGetChargeEvent>(OnGetCharge);
        SubscribeLocalEvent<ModCoreSlotComponent, ChassisTryUseChargeEvent>(OnTryUseCharge);
        SubscribeLocalEvent<ModCoreSlotComponent, ChassisAddChargeEvent>(OnAddCharge);
        SubscribeLocalEvent<ModCoreSlotComponent, ChassisPryEvent>(OnPry);
        SubscribeLocalEvent<ModCoreSlotComponent, ItemSlotEjectAttemptEvent>(OnCoreEjectAttempt);
        SubscribeLocalEvent<ModCoreComponent, AccessibleOverrideEvent>(OnCoreAccessible);
    }

    /// <summary>
    ///     A crowbar in the open panel takes the core and nothing else. It is the one
    ///     piece of hardware that has to come out by hand: everything else is pulled from
    ///     the interface, and the interface is exactly what stops working without a core.
    ///
    ///     The container is emptied directly rather than through <c>TryEject</c>, because
    ///     the eject path is the one <see cref="OnCoreEjectAttempt"/> shuts down.
    /// </summary>
    private void OnPry(Entity<ModCoreSlotComponent> ent, ref ChassisPryEvent args)
    {
        if (args.Handled
            || !_itemSlots.TryGetSlot(ent, ent.Comp.SlotId, out var slot)
            || slot.Item is not { } core
            || slot.ContainerSlot is not { } container)
            return;

        // The tool only asked whether there is anything in there.
        if (args.DryRun)
        {
            args.Handled = true;
            return;
        }

        if (!_container.Remove(core, container))
            return;

        _hands.PickupOrDrop(args.User, core);
        args.Handled = true;
    }

    /// <summary>
    ///     Nothing gets the core out except the crowbar. Cancelling the attempt closes
    ///     every ordinary route at once — the context menu's eject verb, the slot button,
    ///     and smart equip, which otherwise reaches past the suit's pockets and pulls the
    ///     power source out of somebody's back.
    /// </summary>
    private void OnCoreEjectAttempt(Entity<ModCoreSlotComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        // A null user is the game emptying the slot — a destroyed suit handing its core
        // back, say. Only people are being turned away here.
        if (args.User == null || args.Slot.ID != ent.Comp.SlotId)
            return;

        args.Cancelled = true;
    }

    private void OnCoreInserted(Entity<ModCoreSlotComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || !TryComp<ModCoreComponent>(args.Entity, out var core))
            return;

        core.Chassis = ent;
        Dirty(args.Entity, core);

        // The readout has no other notice that its power source came or went.
        AnnounceCharge(ent);
    }

    private void OnCoreRemoved(Entity<ModCoreSlotComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || !TryComp<ModCoreComponent>(args.Entity, out var core))
            return;

        core.Chassis = null;
        Dirty(args.Entity, core);

        // The readout has no other notice that its power source came or went.
        AnnounceCharge(ent);
    }

    /// <summary>
    ///     Tells the chassis its charge situation changed, which is what makes the
    ///     interface redraw.
    /// </summary>
    private void AnnounceCharge(EntityUid chassis)
    {
        var (current, max) = _power.GetCharge(chassis);
        var ev = new ChassisPowerChangedEvent(current, max);
        RaiseLocalEvent(chassis, ref ev);
    }

    /// <summary>
    ///     The core currently installed in this chassis, if any.
    /// </summary>
    public Entity<ModCoreComponent>? GetCore(Entity<ModCoreSlotComponent> ent)
    {
        if (!_itemSlots.TryGetSlot(ent, ent.Comp.SlotId, out var slot)
            || slot.Item is not { } item
            || !TryComp<ModCoreComponent>(item, out var core))
            return null;

        return (item, core);
    }

    /// <summary>
    ///     The cell sitting in a core that takes one, if any. Cores with a sealed battery
    ///     of their own have nothing to give back.
    /// </summary>
    public EntityUid? GetCell(Entity<PowerCellSlotComponent?> core)
    {
        if (!Resolve(core, ref core.Comp, false))
            return null;

        return _itemSlots.TryGetSlot(core, core.Comp.CellSlotId, out var slot) ? slot.Item : null;
    }

    /// <summary>
    ///     Hands the cell back — into the hand, not onto the floor. The core itself stays
    ///     where it is: pulling that is the crowbar's job, and this is the panel.
    /// </summary>
    public bool TryEjectCell(Entity<PowerCellSlotComponent?> core, EntityUid user)
    {
        return Resolve(core, ref core.Comp, false)
               && _itemSlots.TryGetSlot(core, core.Comp.CellSlotId, out var slot)
               && _itemSlots.TryEjectToHands(core, slot, user, excludeUserAudio: true);
    }

    /// <summary>
    ///     Puts whatever is in the hand into the core's cell slot. The slot's own whitelist
    ///     is what refuses a sandwich, so there is nothing to check here.
    /// </summary>
    public bool TryInsertCell(Entity<PowerCellSlotComponent?> core, EntityUid user)
    {
        return Resolve(core, ref core.Comp, false)
               && _itemSlots.TryGetSlot(core, core.Comp.CellSlotId, out var slot)
               && _itemSlots.TryInsertFromHand(core, slot, user, excludeUserAudio: true);
    }

    /// <summary>Whether this core is the kind that runs off a cell you can swap.</summary>
    public bool TakesCell(EntityUid core) => HasComp<PowerCellSlotComponent>(core);

    /// <summary>
    ///     An installed core lives in a sealed slot inside the suit, which by the ordinary
    ///     rules nobody can reach. The panel is a legitimate way in — same reasoning as
    ///     installed modules — so the core borrows the chassis' answer.
    /// </summary>
    private void OnCoreAccessible(Entity<ModCoreComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Target != ent.Owner)
            return;

        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container)
            || !HasComp<ModCoreSlotComponent>(container.Owner))
            return;

        args.Handled = true;
        args.Accessible = _interaction.IsAccessible(args.User, container.Owner);
    }

    private void OnGetCharge(Entity<ModCoreSlotComponent> ent, ref ChassisGetChargeEvent args)
    {
        if (GetCore(ent) is not { } core)
            return;

        if (core.Comp.Infinite)
        {
            args.Current = core.Comp.InfiniteCharge;
            args.Max = core.Comp.InfiniteCharge;
            args.Handled = true;
            return;
        }

        if (!TryGetBattery(core, out var battery))
            return;

        args.Current = _battery.GetCharge((battery.Value.Owner, battery.Value.Comp));
        args.Max = battery.Value.Comp.MaxCharge;
        args.Handled = true;
    }

    private void OnTryUseCharge(Entity<ModCoreSlotComponent> ent, ref ChassisTryUseChargeEvent args)
    {
        if (GetCore(ent) is not { } core)
            return;

        if (core.Comp.Infinite)
        {
            args.Handled = true;
            return;
        }

        if (!TryGetBattery(core, out var battery))
            return;

        args.Handled = _battery.TryUseCharge((battery.Value.Owner, battery.Value.Comp), args.Amount);
    }

    private void OnAddCharge(Entity<ModCoreSlotComponent> ent, ref ChassisAddChargeEvent args)
    {
        if (GetCore(ent) is not { } core)
            return;

        if (core.Comp.Infinite)
        {
            args.Handled = true;
            return;
        }

        if (!TryGetBattery(core, out var battery))
            return;

        _battery.ChangeCharge((battery.Value.Owner, battery.Value.Comp), args.Amount);
        args.Handled = true;
    }

    /// <summary>
    ///     A core either holds a swappable cell in a slot or is a battery in its own right.
    /// </summary>
    private bool TryGetBattery(Entity<ModCoreComponent> core, [NotNullWhen(true)] out Entity<BatteryComponent>? battery)
    {
        battery = null;

        if (HasComp<PowerCellSlotComponent>(core)
            && _powerCell.TryGetBatteryFromSlot(core.Owner, out var slotBattery)
            && slotBattery != null)
        {
            battery = slotBattery.Value;
            return true;
        }

        if (TryComp<BatteryComponent>(core, out var own))
        {
            battery = (core.Owner, own);
            return true;
        }

        return false;
    }
}

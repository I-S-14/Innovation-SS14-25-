// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared.Containers.ItemSlots;
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
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;

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
    }

    /// <summary>
    ///     A crowbar in the open panel takes the core and nothing else. It is the one
    ///     piece of hardware that has to come out by hand: everything else is pulled from
    ///     the interface, and the interface is exactly what stops working without a core.
    /// </summary>
    private void OnPry(Entity<ModCoreSlotComponent> ent, ref ChassisPryEvent args)
    {
        if (args.Handled || !_itemSlots.TryGetSlot(ent, ent.Comp.SlotId, out var slot) || slot.Item == null)
            return;

        // The tool only asked whether there is anything in there.
        if (args.DryRun)
        {
            args.Handled = true;
            return;
        }

        args.Handled = _itemSlots.TryEjectToHands(ent, slot, args.User, excludeUserAudio: true);
    }

    private void OnCoreInserted(Entity<ModCoreSlotComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || !TryComp<ModCoreComponent>(args.Entity, out var core))
            return;

        core.Chassis = ent;
        Dirty(args.Entity, core);
    }

    private void OnCoreRemoved(Entity<ModCoreSlotComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || !TryComp<ModCoreComponent>(args.Entity, out var core))
            return;

        core.Chassis = null;
        Dirty(args.Entity, core);
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

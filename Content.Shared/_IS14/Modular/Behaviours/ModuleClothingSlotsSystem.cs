// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Lets a module change where the chassis may be worn.
/// </summary>
public sealed class ModuleClothingSlotsSystem : ModuleBehaviourSystem<ModuleClothingSlotsComponent>
{
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleClothingSlotsComponent, ChassisUninstallModuleAttemptEvent>(OnUninstallAttempt);
    }

    /// <summary>
    ///     Refuses to come out while the chassis is hanging somewhere only this module
    ///     allows. Restoring the old slots does not unequip what is already worn, so
    ///     pulling the module off a belt-slung suit left it on the belt with its
    ///     complexity handed back — the compression paid for, then refunded.
    /// </summary>
    private void OnUninstallAttempt(Entity<ModuleClothingSlotsComponent> ent, ref ChassisUninstallModuleAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.Previous is not { } previous)
            return;

        if (!_inventory.TryGetContainingSlot(args.Chassis, out var slot))
            return;

        // Same test the inventory itself equips by: the garment's flags have to cover
        // the slot's. If they still would without us, this module is not what is
        // holding the chassis there and has no business refusing.
        if (previous.HasFlag(slot.SlotFlags))
            return;

        // Answer only — the popup belongs to whoever asked, because the interface asks
        // this same question every refresh and must not shout while doing it.
        args.Cancelled = true;
        args.Reason = "chassis-module-slots-in-use";
    }

    /// <summary>
    ///     Follows installation rather than the chassis running, and it has to: the module
    ///     changes where the suit may be worn, and a suit has to be worn before it can be
    ///     switched on. Gating this on the suit being live asked the player to hang the MOD
    ///     off their belt before installing the module that lets them — which is nowhere.
    /// </summary>
    protected override bool FollowsInstallation(Entity<ModuleClothingSlotsComponent> ent) => true;

    protected override void Start(Entity<ModuleClothingSlotsComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Previous != null || !TryComp<ClothingComponent>(chassis, out var clothing))
            return;

        ent.Comp.Previous = clothing.Slots;
        _clothing.SetSlots(chassis, clothing.Slots | ent.Comp.Slots, clothing);
    }

    protected override void Stop(Entity<ModuleClothingSlotsComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Previous is not { } previous)
            return;

        ent.Comp.Previous = null;

        if (!TerminatingOrDeleted(chassis) && TryComp<ClothingComponent>(chassis, out var clothing))
            _clothing.SetSlots(chassis, previous, clothing);
    }
}

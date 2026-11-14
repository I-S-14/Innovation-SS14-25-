// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Containers.ItemSlots;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.IdentityManagement;

namespace Content.Server._EinsteinEngines.Silicons.BatteryLocking;

public sealed class BatterySlotRequiresLockSystem : EntitySystem

{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BatterySlotRequiresLockComponent, LockToggledEvent>(LockToggled);
        SubscribeLocalEvent<BatterySlotRequiresLockComponent, LockToggleAttemptEvent>(LockToggleAttempted);

    }
    private void LockToggled(EntityUid uid, BatterySlotRequiresLockComponent component, ref LockToggledEvent args)
    {
        if (!TryComp<LockComponent>(uid, out var lockComp)
            || !TryComp<ItemSlotsComponent>(uid, out var itemslots)
            || !_itemSlotsSystem.TryGetSlot((uid, itemslots), component.ItemSlot, out var slot))
            return;

        _itemSlotsSystem.SetLock((uid, itemslots), slot, lockComp.Locked);
    }

    private void LockToggleAttempted(EntityUid uid, BatterySlotRequiresLockComponent component, ref LockToggleAttemptEvent args)
    {
        if (args.User == uid || !HasComp<SiliconComponent>(uid))
            return;

        _popupSystem.PopupEntity(Loc.GetString("batteryslotrequireslock-component-alert-owner", ("user", Identity.Entity(args.User, EntityManager))), uid, uid, PopupType.Large);
    }

}
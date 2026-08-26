// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.Inventory;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Client.Player;

namespace Content.Client._IS14.Modular;

/// <summary>
///     Keeps the little pocket glyph on an equipment slot honest.
///
///     The inventory hotbar decides whether to draw it once, when the item is equipped,
///     and never asks again. That is fine for a bag, which either is one or is not — but
///     a chassis grows and loses its compartments while it is being worn, and the slot
///     was left showing the answer from whenever the suit went on.
/// </summary>
public sealed class ChassisStorageBadgeSystem : EntitySystem
{
    [Dependency] private readonly ClientInventorySystem _clientInventory = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    /// <summary>
    ///     Chassis waiting for a glyph check. The module container change and the storage
    ///     component's own arrival come out of the same server tick with no ordering
    ///     between them, so asking on the spot can read the state from before the change.
    /// </summary>
    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularChassisComponent, ChassisModulesChangedEvent>(OnModulesChanged);
    }

    private void OnModulesChanged(Entity<ModularChassisComponent> ent, ref ChassisModulesChangedEvent args)
    {
        _pending.Add(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        foreach (var chassis in _pending)
        {
            Refresh(chassis);
        }

        _pending.Clear();
    }

    private void Refresh(EntityUid chassis)
    {
        // Only the local player has a hotbar to correct.
        if (_player.LocalEntity is not { } player || !Exists(chassis))
            return;

        if (!_inventory.TryGetContainingSlot(chassis, out var slot))
            return;

        // Somebody else's suit changing its modules is not our hotbar's business.
        if (!_inventory.TryGetSlotEntity(player, slot.Name, out var worn) || worn != chassis)
            return;

        _clientInventory.OnSpriteUpdate?.Invoke(new ClientInventorySystem.SlotSpriteUpdate(
            chassis,
            slot.SlotGroup,
            slot.Name,
            HasComp<StorageComponent>(chassis)));
    }
}

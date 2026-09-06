using Content.Shared._IS14.OS.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.OS;

/// <summary>
///     Gives the crew the fork's own PDAs without editing a single upstream loadout.
///
///     The swap runs on <see cref="StartingGearEquippedEvent"/>, which fires after the gear is
///     on but *before* the spawner writes the ID card and binds the PDA to its owner — so the
///     replacement gets named, given access and bound by the stock code path, exactly like any
///     other PDA. No binding logic is duplicated here.
/// </summary>
public sealed class IS14PdaSwapSystem : EntitySystem
{
    private const string IdSlot = "id";

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private readonly Dictionary<string, EntProtoId> _swaps = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, StartingGearEquippedEvent>(OnGearEquipped);

        BuildTable();
        _proto.PrototypesReloaded += _ => BuildTable();
    }

    /// <summary>Every table is merged, so a later file can extend the mapping.</summary>
    private void BuildTable()
    {
        _swaps.Clear();

        foreach (var set in _proto.EnumeratePrototypes<IS14PdaSwapPrototype>())
        {
            foreach (var (from, to) in set.Swaps)
                _swaps[from.Id] = to;
        }
    }

    private void OnGearEquipped(Entity<InventoryComponent> ent, ref StartingGearEquippedEvent args)
    {
        if (!_inventory.TryGetSlotEntity(ent, IdSlot, out var current, ent.Comp))
            return;

        if (Prototype(current.Value)?.ID is not { } proto || !_swaps.TryGetValue(proto, out var replacement))
            return;

        var coords = Transform(ent).Coordinates;
        var pda = Spawn(replacement, coords);

        if (!_inventory.TryUnequip(ent, IdSlot, silent: true, force: true, inventory: ent.Comp))
        {
            // Something is holding the slot. Better a stray PDA on the floor than none at all.
            QueueDel(pda);
            return;
        }

        if (!_inventory.TryEquip(ent, pda, IdSlot, silent: true, force: true, inventory: ent.Comp))
        {
            // Put the original back rather than leaving the crewman with no PDA and no ID.
            _inventory.TryEquip(ent, current.Value, IdSlot, silent: true, force: true, inventory: ent.Comp);
            QueueDel(pda);
            return;
        }

        // The stock PDA takes its blank ID card with it; the replacement brought its own, and
        // the spawner is about to fill that one in.
        QueueDel(current.Value);
    }
}

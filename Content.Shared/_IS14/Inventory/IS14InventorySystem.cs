// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._IS14.Inventory;

/// <summary>
///     Relay registrations for IS14 components worn in inventory slots.
///
///     <see cref="InventorySystem.RelayEvent"/> only fires for event types that were
///     explicitly subscribed on <see cref="InventoryComponent"/> — a generic event with no
///     subscription here simply never reaches the worn item, and whatever depends on it
///     silently does nothing. Upstream keeps its list in InventorySystem.Relay.cs and Goob
///     keeps its own in GoobInventorySystem.Relays.cs; this is the IS14 one, so no upstream
///     file has to be touched to add a component.
/// </summary>
public sealed class IS14InventorySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<StructuralVisionComponent>>(RefRelay);
    }

    private void RefRelay<T>(EntityUid uid, InventoryComponent component, ref T args) where T : IInventoryRelayEvent
    {
        _inventory.RelayEvent((uid, component), ref args);
    }
}

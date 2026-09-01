// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modsuit.Behaviours;

/// <summary>
///     Hangs one item slot off a piece of the suit's plating for as long as the module is
///     installed.
///
///     Not storage: a holster is a place for one thing that everyone can see, and a
///     storage grid with a whitelist is a different object with a different feel — it has
///     a window, it takes a click to open, and it holds whatever fits. A slot on the
///     chestplate is the sidearm on your chest.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleSuitSlotComponent : Component
{
    /// <summary>
    ///     Which piece of plating carries the slot, by the suit's own slot name.
    /// </summary>
    [DataField]
    public string Part = "outerClothing";

    /// <summary>
    ///     Container id for the slot. Must be unique on the part.
    /// </summary>
    [DataField]
    public string SlotId = "modsuit-module-slot";

    /// <summary>
    ///     The slot itself — name, whitelist, sounds. Written straight into the part.
    /// </summary>
    [DataField(required: true)]
    public ItemSlot Slot = new();

    /// <summary>
    ///     Whether whatever is in the slot is drawn over the part on the wearer. A hat
    ///     cradle exists so people can see the hat; a holster on the chestplate would
    ///     rather not paint a pistol across the sprite.
    /// </summary>
    [DataField]
    public bool ShowContents;

    /// <summary>
    ///     Where an item has to be wearable before the slot will take it. NONE accepts
    ///     anything the whitelist lets through, which is what a holster wants — it does
    ///     not care where the gun would otherwise be worn.
    ///
    ///     Separate from the whitelist because a whitelist can only ask what components
    ///     an item has, and every garment on the station has <c>Clothing</c>.
    /// </summary>
    [DataField]
    public SlotFlags WearableIn = SlotFlags.NONE;

    /// <summary>
    ///     Where the slot was actually put, so it comes back off the right piece even if
    ///     the plating changed underneath.
    /// </summary>
    [ViewVariables]
    public EntityUid? GrantedTo;
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Item;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Turns the chassis into a container while installed — the storage module, the ore
///     bag, the holster. The storage lives on the chassis itself rather than on the module
///     so its contents survive the module being swapped for a bigger one.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleStorageComponent : Component
{
    /// <summary>
    ///     Storage grid granted to the chassis, in the same form
    ///     <see cref="Content.Shared.Storage.StorageComponent.Grid"/> uses.
    /// </summary>
    [DataField(required: true)]
    public List<Box2i> Grid = new();

    /// <summary>
    ///     Grid the chassis had before this module took over, so removing the module
    ///     restores it rather than leaving a phantom bag behind.
    /// </summary>
    [ViewVariables]
    public List<Box2i>? PreviousGrid;

    [ViewVariables]
    public EntityWhitelist? PreviousWhitelist;

    [ViewVariables]
    public ProtoId<ItemSizePrototype>? PreviousMaxItemSize;

    [ViewVariables]
    public bool Applied;

    /// <summary>
    ///     Whether this module is the reason the chassis has storage at all, so removing
    ///     it takes the pocket away again instead of leaving an empty one behind.
    /// </summary>
    [ViewVariables]
    public bool Granted;

    /// <summary>
    ///     Restricts what the compartments accept. An ore satchel is the same machinery
    ///     as a pocket with a narrower mouth.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    ///     Whether the grid is hung off the chassis or off the module itself.
    ///
    ///     On the chassis it is *the* suit's pockets: one per suit, opened with E, and
    ///     surviving a swap for a bigger module. On the module it is a satchel of its own,
    ///     which is what lets a specialised bag sit alongside the general one instead of
    ///     fighting it for the single storage component an entity is allowed.
    /// </summary>
    [DataField]
    public bool OnChassis = true;

    /// <summary>
    ///     Largest item the compartments take. Worth setting whenever the grid lives on
    ///     the module: storage with no explicit limit derives its limit from its own item
    ///     size, and a module is small, so an unset satchel refuses an ordinary lump of ore.
    /// </summary>
    [DataField]
    public ProtoId<ItemSizePrototype>? MaxItemSize;

    /// <summary>
    ///     Entity the grid was actually granted to, so it is taken off the same one.
    /// </summary>
    [ViewVariables]
    public EntityUid? Host;
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

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
    public bool Applied;

    /// <summary>
    ///     Whether this module is the reason the chassis has storage at all, so removing
    ///     it takes the pocket away again instead of leaving an empty one behind.
    /// </summary>
    [ViewVariables]
    public bool Granted;
}

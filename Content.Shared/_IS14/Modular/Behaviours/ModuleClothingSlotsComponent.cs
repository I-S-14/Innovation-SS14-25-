// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Widens the set of slots the chassis itself can be worn in. A suit that folds
///     small enough to hang off a belt is a real tradeoff rather than a straight upgrade:
///     the volume has to come from somewhere, which is why compression and compartments
///     do not coexist.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleClothingSlotsComponent : Component
{
    /// <summary>
    ///     Slots added to the chassis' own clothing flags while installed.
    /// </summary>
    [DataField(required: true)]
    public SlotFlags Slots = SlotFlags.NONE;

    /// <summary>
    ///     Flags the chassis had before, so removal puts them back exactly.
    /// </summary>
    [ViewVariables]
    public SlotFlags? Previous;
}

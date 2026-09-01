// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     A MOD core: the thing that answers a chassis' charge questions.
///     A core with a <c>PowerCellSlot</c> runs off a swappable battery; a core with a
///     <c>Battery</c> of its own is sealed. <see cref="Infinite"/> covers admin gear.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ModCoreSystem))]
public sealed partial class ModCoreComponent : Component
{
    /// <summary>
    ///     Chassis this core is installed in.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Chassis;

    /// <summary>
    ///     Never runs out and never needs charging.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Infinite;

    /// <summary>
    ///     Charge reported while <see cref="Infinite"/> is set, purely so the UI has
    ///     a sane bar to draw.
    /// </summary>
    [DataField]
    public float InfiniteCharge = 10000f;
}

/// <summary>
///     Marks the item slot on a chassis that accepts a <see cref="ModCoreComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ModCoreSystem))]
public sealed partial class ModCoreSlotComponent : Component
{
    /// <summary>
    ///     Id of the <c>ItemSlots</c> slot the core sits in.
    /// </summary>
    [DataField]
    public string SlotId = "mod-core";
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Sweeps loose items off the floor into the module's own satchel while it is switched
///     on. Pairs with <see cref="ModuleStorageComponent"/>, which decides what the bag will
///     take: the magnet has no opinion of its own, it just reaches.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleMagnetComponent : Component
{
    /// <summary>
    ///     How far the field reaches, in tiles. Short on purpose — this is meant to save
    ///     a miner the click, not to strip a room from the doorway.
    /// </summary>
    [DataField]
    public float Range = 2f;

    /// <summary>
    ///     Seconds between sweeps. The magnet is a convenience, and a convenience that
    ///     runs on every tick is a lag source.
    /// </summary>
    [DataField]
    public float Interval = 1f;

    /// <summary>
    ///     Charge spent per sweep that actually picked something up. Sweeping an empty
    ///     floor is free — there is nothing to lift.
    /// </summary>
    [DataField]
    public float Cost = 5f;

    /// <summary>When the next sweep is due.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextScan;

    [ViewVariables]
    public bool Running;
}

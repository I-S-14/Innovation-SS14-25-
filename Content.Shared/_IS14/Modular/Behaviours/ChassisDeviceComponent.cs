// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Marks a device entity as belonging to a module — the analyzer that folds out of a
///     gauntlet, the drill, the paddles. The hardware is the suit's, not the wearer's, so
///     it cannot be dropped, thrown or pocketed: any attempt reels it back into the module.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChassisDeviceComponent : Component
{
    /// <summary>
    ///     Module this device folds back into.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Module;

    /// <summary>
    ///     Set while the module itself is moving the device, so the reel-in guard stands
    ///     aside for the one removal that is supposed to happen.
    /// </summary>
    [ViewVariables]
    public bool Stowing;
}

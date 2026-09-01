// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Thruster ports for the chassis, burning the suit's own bottle.
///
///     Not a plain component grant: a jetpack is two halves, the hardware and the switch,
///     and the switch is normally a hotbar action handed out when the pack is equipped.
///     Nothing hands one out for a component that appeared mid-round, so the module works
///     the switch itself — its own on/off is the jetpack's on/off.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleJetpackComponent : Component
{
    /// <summary>
    ///     Moles burned per thrust pulse. An order of magnitude below the engine default,
    ///     because the suit's bottle is a fraction of the size of a real jetpack's.
    /// </summary>
    [DataField]
    public float MoleUsage = 0.0015f;

    [DataField]
    public float Acceleration = 1f;

    [DataField]
    public float Friction = 0.25f;

    [DataField]
    public float WeightlessModifier = 1.2f;

    [ViewVariables]
    public bool Applied;
}

/// <summary>
///     Sits on the wearer while a jetpack module is switched on, so the suit hears about
///     it the moment they stop having a floor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChassisJetpackUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Chassis;
}


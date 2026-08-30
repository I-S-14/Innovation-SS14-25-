// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Holds the wearer at a temperature they picked, rather than merely slowing down
///     whatever the room is doing to them.
///
///     Separate from the temperature protection the same module grants: insulation
///     decides how fast the outside gets in, this decides where the inside settles.
///     A suit that only insulates leaves a wearer who has already been cooked or frozen
///     exactly as cooked or frozen as they were.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleThermalRegulatorComponent : Component
{
    /// <summary>
    ///     Temperature the loop drives towards, in kelvin.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Target = Atmospherics.T20C + 17f;

    /// <summary>
    ///     Coldest the wearer may set it to. Below room temperature is refrigeration,
    ///     not comfort, and the module is not built for it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinTarget = Atmospherics.T20C;

    /// <summary>
    ///     Hottest it will go. Past this the wearer is cooking themselves on purpose,
    ///     which the suit declines to help with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxTarget = Atmospherics.T20C + 25f;

    /// <summary>
    ///     Kelvin per second the loop can move the wearer. Deliberately unhurried: this
    ///     is a comfort system that wins slowly against a bad room, not a way to shrug
    ///     off standing in a fire.
    /// </summary>
    [DataField]
    public float Rate = 1.5f;

    /// <summary>
    ///     How close counts as arrived, so the loop stops rather than hunting around the
    ///     setpoint forever.
    /// </summary>
    [DataField]
    public float Tolerance = 0.5f;

    /// <summary>
    ///     Set while the loop is running, so the update pass can skip every module that
    ///     is installed but switched off without resolving a wearer for each.
    /// </summary>
    [ViewVariables]
    public bool Running;
}

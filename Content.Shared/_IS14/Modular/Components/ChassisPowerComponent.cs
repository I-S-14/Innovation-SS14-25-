// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Components;

/// <summary>
///     Marks a chassis as drawing power, without saying where that power comes from.
///     The actual charge lives behind <see cref="ChassisGetChargeEvent"/> and friends,
///     which a core, a battery slot or a mech's internal cell can all answer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
// The modsuit lock system drives Malfunctioning from the wire panel.
[Access(typeof(ChassisPowerSystem), typeof(Content.Shared._IS14.Modsuit.Systems.SharedModsuitLockSystem))]
public sealed partial class ChassisPowerComponent : Component
{
    /// <summary>
    ///     Baseline draw in watts while the chassis is active, before module draw.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BaseDraw = 0.5f;

    /// <summary>
    ///     Multiplier applied to all draw. Themes use this to be thriftier or hungrier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DrawMultiplier = 1f;

    /// <summary>
    ///     Extra multiplier applied while the chassis is malfunctioning.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MalfunctionDrawMultiplier = 3f;

    /// <summary>
    ///     Set by wire sabotage or EMP. Multiplies draw and lets modules cut out at random.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Malfunctioning;

    /// <summary>
    ///     Charge is spent in whole seconds rather than every tick, so a suit full of
    ///     modules does not thrash the battery. This accumulates the fractional remainder.
    /// </summary>
    [ViewVariables]
    public float Accumulator;

    /// <summary>
    ///     Fraction of maximum charge at which the wearer gets a low power warning.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LowChargeWarningFraction = 0.1f;

    /// <summary>
    ///     Set once the low charge warning has fired, cleared when charge recovers.
    /// </summary>
    [ViewVariables]
    public bool LowChargeWarned;
}

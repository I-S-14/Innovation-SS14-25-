// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Screens the chassis' power train against induced pulses while installed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleEmpShieldComponent : Component;

/// <summary>
///     Put on a chassis by an installed <see cref="ModuleEmpShieldComponent"/>. Anything
///     nested inside the chassis is inside the screen.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ChassisEmpShieldComponent : Component;

/// <summary>
///     Bookkeeping for an entity whose EMP resistance is currently coming from a screen
///     rather than from its own prototype. Remembers what the resistance was before the
///     screen touched it, so leaving the suit puts the entity back where it started.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChassisEmpScreenedComponent : Component
{
    /// <summary>
    ///     Whether the entity carried an <c>EmpResistance</c> of its own, which the screen
    ///     multiplied instead of replaced.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HadResistance;

    [DataField, AutoNetworkedField]
    public float PrevStrength = 1f;

    [DataField, AutoNetworkedField]
    public float PrevDuration = 1f;
}

using Robust.Shared.GameStates;

namespace Content.Shared.Emp;

/// <summary>
/// An entity with this component resists or is fully immune to EMPs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//IS14-change start: the MOD screen damps a pulse by writing resistance onto whatever
// is actually being pulsed — the cell nested inside the suit — so it needs write access.
[Access(typeof(SharedEmpSystem), typeof(Content.Shared._IS14.Modular.Behaviours.ModuleEmpShieldSystem))]
//IS14-change end
public sealed partial class EmpResistanceComponent : Component
{
    /// <summary>
    /// The strength of the EMP gets multiplied by this value.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StrengthMultiplier = 1f;

    /// <summary>
    /// The duration of the EMP gets multiplied by this value.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DurationMultiplier = 1f;
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Shoulder-mounted deterrent. Anyone who lays hands on the wearer gets a face full
///     of it — the module never fires on command, only in answer, which is what keeps it
///     a defence rather than a grenade you wear.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModulePepperComponent : Component
{
    /// <summary>
    ///     Burst spawned at the wearer. A self-triggering cloud, so the module itself
    ///     carries no chemistry.
    /// </summary>
    [DataField]
    public EntProtoId Burst = "IS14ModsuitPepperBurst";
}

/// <summary>
///     Put on the wearer while a pepper module is running, so the blows aimed at them
///     can find the module that answers.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ChassisPepperGuardComponent : Component
{
    [ViewVariables]
    public EntityUid Module;
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Lights the chassis up while the module is switched on — the flashlight module,
///     and the flashdark that runs the same machinery with a negative radius.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleLightComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField, AutoNetworkedField]
    public float Radius = 4.5f;

    [DataField, AutoNetworkedField]
    public float Energy = 3f;

    [DataField, AutoNetworkedField]
    public float Softness = 1f;

    /// <summary>
    ///     Set while this module is the one lighting the chassis, so switching it off
    ///     does not strip a light some other module or the suit itself provided.
    /// </summary>
    [ViewVariables]
    public bool Applied;
}

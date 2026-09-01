// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Gives the chassis operator an action while the module is running, and takes it
///     back when it stops. The action's own event is handled by whatever system owns it,
///     so this behaviour stays ignorant of what the action does.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleActionComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action = default!;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    ///     Follow the module's on/off switch rather than merely being installed.
    /// </summary>
    [DataField]
    public bool RequireActive;

    /// <summary>
    ///     Who currently holds the action, so it can be taken off the right person.
    /// </summary>
    [ViewVariables]
    public EntityUid? GrantedTo;
}

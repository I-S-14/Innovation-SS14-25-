// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     Grants the wearer the suit's control actions while it is equipped.
///     Kept separate from <see cref="ModsuitControlComponent"/> so a suit can opt out of
///     any of them, and so the same pattern can be lifted for other chassis later.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedModsuitSystem))]
public sealed partial class ModsuitActionsComponent : Component
{
    [DataField]
    public EntProtoId? DeployAction = "ActionModsuitToggleDeploy";

    [DataField, AutoNetworkedField]
    public EntityUid? DeployActionEntity;

    [DataField]
    public EntProtoId? SealAction = "ActionModsuitToggleSeal";

    [DataField, AutoNetworkedField]
    public EntityUid? SealActionEntity;

    [DataField]
    public EntProtoId? ModulesAction = "ActionModsuitOpenModules";

    [DataField, AutoNetworkedField]
    public EntityUid? ModulesActionEntity;

    [DataField]
    public EntProtoId? PanelAction = "ActionModsuitOpenPanel";

    [DataField, AutoNetworkedField]
    public EntityUid? PanelActionEntity;

    /// <summary>
    ///     When the armed seal button stops counting as armed.
    ///
    ///     Sealing and unsealing are both slow, loud and occasionally fatal — unsealing
    ///     rather more so, since the room decides what happens next. One press arms the
    ///     button and says so, the second one commits. A fat-fingered hotkey costs a
    ///     glance at the icon rather than a lungful of whatever is outside.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan? SealArmedUntil;

    /// <summary>
    ///     How long the button stays armed before it forgets. Long enough to be a
    ///     confirmation, short enough that it is never still armed later.
    /// </summary>
    [DataField]
    public TimeSpan SealArmWindow = TimeSpan.FromSeconds(3);
}

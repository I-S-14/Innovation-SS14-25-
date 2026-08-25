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
}

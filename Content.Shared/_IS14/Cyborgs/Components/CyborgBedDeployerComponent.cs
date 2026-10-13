// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's silicon emergency bed: click the floor to deploy a roller bed, click the bed
/// with the tool to pack it back. One bed out at a time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyborgBedDeployerComponent : Component
{
    [DataField]
    public EntProtoId Bed = "RollerBed";

    [DataField, AutoNetworkedField]
    public EntityUid? Deployed;
}

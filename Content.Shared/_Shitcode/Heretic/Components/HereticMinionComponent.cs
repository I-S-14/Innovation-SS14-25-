using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitcode.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticMinionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? BoundHeretic;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<StatusIconPrototype> MasterIcon { get; set; } = "GhoulHereticMaster";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<StatusIconPrototype> GhoulIcon { get; set; } = "GhoulFaction";
}

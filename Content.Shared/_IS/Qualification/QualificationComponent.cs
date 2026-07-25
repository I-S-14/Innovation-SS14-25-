using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS.Qualification.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class QualificationComponent : Component
{
    //[DataField, AutoNetworkedField]
    //public LocId QualificationTitle = "IS14-Qulification-Unknown";

    [DataField, AutoNetworkedField]
    public ProtoId<QualificationPrototype>? QualificationIcon = null;
}

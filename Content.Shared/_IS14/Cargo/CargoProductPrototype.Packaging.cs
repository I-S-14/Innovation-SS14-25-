using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Prototypes;

public sealed partial class CargoProductPrototype
{
    /// <summary>Optional Wizards parcel around a delivered product.</summary>
    [DataField]
    public CargoProductContainer? Container;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CargoProductContainer
{
    [DataField(required: true)]
    public EntProtoId<ContainerManagerComponent> Entity;

    [DataField(required: true)]
    public string ContainerId = default!;
}

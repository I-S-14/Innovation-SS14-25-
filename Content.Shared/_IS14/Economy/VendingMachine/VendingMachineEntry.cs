using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Economy.VendingMachine;

[DataDefinition]
public sealed partial class VendingMachineEntry
{
    [DataField(required: true)]
    public EntProtoId ItemId;

    [DataField]
    public int Price;

    [DataField]
    public int Stock = 5;
}

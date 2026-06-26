namespace Content.Shared._IS14.Economy.VendingMachine;

[DataDefinition]
public sealed partial class VendingMachineTab
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public List<VendingMachineEntry> Inventory = new();
}

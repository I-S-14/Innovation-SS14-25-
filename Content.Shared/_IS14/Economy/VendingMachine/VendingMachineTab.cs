using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Economy.VendingMachine;

[DataDefinition]
public sealed partial class VendingMachineTab
{
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>
    /// Access levels required to buy from this tab: the buyer's ID needs at least
    /// one of the listed accesses. Empty list means the tab is available to everyone.
    /// </summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> Access = new();

    [DataField]
    public List<VendingMachineEntry> Inventory = new();
}

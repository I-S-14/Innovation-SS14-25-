using Robust.Shared.Audio;

namespace Content.Shared._IS14.Economy.VendingMachine;

[RegisterComponent]
public sealed partial class IS14VendingMachineComponent : Component
{
    [DataField]
    public string MachineName = "Торговый автомат";

    [DataField]
    public List<VendingMachineTab> Tabs = new();

    [DataField]
    public List<VendingMachineEntry> ContrabandInventory = new();

    [DataField]
    public List<LocId> AdMessages = new();

    public bool ContrabandUnlocked;

    [DataField]
    public SoundSpecifier BuySound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
}

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.VendingMachine;

[Serializable, NetSerializable]
public sealed class IS14VendingTabUiState
{
    public readonly string Name;
    public readonly List<IS14VendingMachineUiEntry> Inventory;

    public IS14VendingTabUiState(string name, List<IS14VendingMachineUiEntry> inventory)
    {
        Name = name;
        Inventory = inventory;
    }
}

[Serializable, NetSerializable]
public sealed class IS14VendingMachineUiState : BoundUserInterfaceState
{
    public readonly string MachineName;
    public readonly int Balance;
    public readonly bool ContrabandUnlocked;
    public readonly List<IS14VendingTabUiState> Tabs;
    public readonly List<IS14VendingMachineUiEntry> Contraband;
    public readonly string PlayerName;
    public readonly string PlayerJob;
    public readonly string AdMessage;

    public IS14VendingMachineUiState(
        string machineName,
        int balance,
        bool contrabandUnlocked,
        List<IS14VendingTabUiState> tabs,
        List<IS14VendingMachineUiEntry> contraband,
        string playerName,
        string playerJob,
        string adMessage)
    {
        MachineName = machineName;
        Balance = balance;
        ContrabandUnlocked = contrabandUnlocked;
        Tabs = tabs;
        Contraband = contraband;
        PlayerName = playerName;
        PlayerJob = playerJob;
        AdMessage = adMessage;
    }
}

[Serializable, NetSerializable]
public sealed class IS14VendingMachineUiEntry
{
    public EntProtoId ItemId;
    public string ItemName;
    public int Price;
    public int Stock;

    public IS14VendingMachineUiEntry(EntProtoId itemId, string itemName, int price, int stock)
    {
        ItemId = itemId;
        ItemName = itemName;
        Price = price;
        Stock = stock;
    }
}

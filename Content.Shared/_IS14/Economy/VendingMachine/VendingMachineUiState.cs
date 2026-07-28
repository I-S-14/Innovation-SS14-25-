using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.VendingMachine;

[Serializable, NetSerializable]
public sealed class IS14VendingTabUiState
{
    public readonly string Name;
    public readonly List<IS14VendingMachineUiEntry> Inventory;
    /// <summary>Whether the viewing player's ID satisfies the tab's access requirements.</summary>
    public readonly bool HasAccess;

    public IS14VendingTabUiState(string name, List<IS14VendingMachineUiEntry> inventory, bool hasAccess)
    {
        Name = name;
        Inventory = inventory;
        HasAccess = hasAccess;
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
    /// <summary>The ID card the balance is read from; used to render its sprite in the UI.</summary>
    public readonly NetEntity? IdCard;
    public readonly string AdMessage;

    public IS14VendingMachineUiState(
        string machineName,
        int balance,
        bool contrabandUnlocked,
        List<IS14VendingTabUiState> tabs,
        List<IS14VendingMachineUiEntry> contraband,
        string playerName,
        string playerJob,
        NetEntity? idCard,
        string adMessage)
    {
        MachineName = machineName;
        Balance = balance;
        ContrabandUnlocked = contrabandUnlocked;
        Tabs = tabs;
        Contraband = contraband;
        PlayerName = playerName;
        PlayerJob = playerJob;
        IdCard = idCard;
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

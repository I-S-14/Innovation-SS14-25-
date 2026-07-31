using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.VendingMachine;

[Serializable, NetSerializable]
public sealed class IS14VendingMachineBuyMessage : BoundUserInterfaceMessage
{
    /// <summary>Index into the Tabs list. -1 means contraband.</summary>
    public readonly int TabIndex;
    public readonly int ItemIndex;

    public IS14VendingMachineBuyMessage(int tabIndex, int itemIndex)
    {
        TabIndex = tabIndex;
        ItemIndex = itemIndex;
    }
}

/// <summary>Hands the cash left in the machine back to the customer.</summary>
[Serializable, NetSerializable]
public sealed class IS14VendingMachineEjectCashMessage : BoundUserInterfaceMessage
{
}

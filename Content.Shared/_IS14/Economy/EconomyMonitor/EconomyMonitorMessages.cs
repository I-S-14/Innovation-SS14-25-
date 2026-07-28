using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.EconomyMonitor;

/// <summary>Deletes the selected records from the monitor server's log.</summary>
[Serializable, NetSerializable]
public sealed class EconomyMonitorDeleteMessage : BoundUserInterfaceMessage
{
    public readonly List<int> RecordIds;

    public EconomyMonitorDeleteMessage(List<int> recordIds)
    {
        RecordIds = recordIds;
    }
}

/// <summary>Prints the selected records as a paper report at the console.</summary>
[Serializable, NetSerializable]
public sealed class EconomyMonitorPrintMessage : BoundUserInterfaceMessage
{
    public readonly List<int> RecordIds;

    public EconomyMonitorPrintMessage(List<int> recordIds)
    {
        RecordIds = recordIds;
    }
}

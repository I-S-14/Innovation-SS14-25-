using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.EconomyMonitor;

[Serializable, NetSerializable]
public sealed class EconomyMonitorUiState : BoundUserInterfaceState
{
    /// <summary>Transactions ordered newest first.</summary>
    public readonly List<EconomyTransactionRecord> Records;

    /// <summary>All trackable vendor devices on the station.</summary>
    public readonly List<EconomyVendorBlipInfo> Vendors;

    /// <summary>Grid entity that hosts the station map (for NavMapControl).</summary>
    public readonly NetEntity? GridEntity;

    public EconomyMonitorUiState(
        List<EconomyTransactionRecord> records,
        List<EconomyVendorBlipInfo> vendors,
        NetEntity? gridEntity)
    {
        Records = records;
        Vendors = vendors;
        GridEntity = gridEntity;
    }
}

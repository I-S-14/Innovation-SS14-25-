using Content.Shared._IS14.Economy.EconomyMonitor;

namespace Content.Server._IS14.Economy.EconomyMonitor;

/// <summary>
/// Placed on the economy monitor server entity. Stores the transaction log
/// and connects to consoles via <see cref="NetworkId"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EconomyMonitorServerComponent : Component
{
    /// <summary>
    /// Shared ID between this server and its consoles.
    /// Consoles with the same NetworkId will display this server's log.
    /// </summary>
    [DataField]
    public string NetworkId = "default";

    /// <summary>Maximum number of records kept in the log.</summary>
    [DataField]
    public int MaxEntries = 500;

    /// <summary>Transaction log, oldest first.</summary>
    public List<EconomyTransactionRecord> Log = new();
}

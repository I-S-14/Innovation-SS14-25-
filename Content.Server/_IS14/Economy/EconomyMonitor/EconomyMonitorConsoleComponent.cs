namespace Content.Server._IS14.Economy.EconomyMonitor;

/// <summary>
/// Placed on the economy monitor console entity.
/// The console displays log data from the <see cref="EconomyMonitorServerComponent"/>
/// with the matching <see cref="NetworkId"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EconomyMonitorConsoleComponent : Component
{
    /// <summary>Must match the target server's NetworkId.</summary>
    [DataField]
    public string NetworkId = "default";
}

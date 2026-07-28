using Content.Shared._IS14.Economy.Fines;

namespace Content.Server._IS14.Economy.Fines;

/// <summary>
/// Every fine written on this station. Added on demand the first time an officer
/// writes one.
/// </summary>
[RegisterComponent]
public sealed partial class StationFinesComponent : Component
{
    public List<FineRecord> Fines = new();

    public uint NextFineId = 1;

    /// <summary>
    /// Station records whose wanted status was set by an unpaid fine rather than by an
    /// officer. Only these get cleared automatically once the debt is settled, so paying
    /// a fine can never wipe a real manhunt.
    /// </summary>
    public HashSet<uint> WantedByFine = new();
}

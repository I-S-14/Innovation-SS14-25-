using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.OS.Components;

/// <summary>
///     Removable media (Docs/_IS14/os-design.md §7.4).
///
///     The reason software ships on a physical object rather than only through the store is
///     that an object can be stolen, hidden, planted and destroyed. A department's disk in a
///     locker is a thing worth taking; a download is not.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsDiskComponent : Component
{
    /// <summary>Slot on a device a disk goes into.</summary>
    public const string SlotId = "os_disk";

    /// <summary>Software on the disk, installable onto whatever it is plugged into.</summary>
    [DataField]
    public List<ProtoId<IS14OsAppPrototype>> Apps = new();

    /// <summary>Documents and photos carried on the disk.</summary>
    [DataField]
    public List<OsFile> Files = new();

    [DataField]
    public int NextFileId = 1;

    /// <summary>How much file data the disk holds, in GQ. Software on it does not count.</summary>
    [DataField]
    public int Capacity = 24;

    /// <summary>Read-only disks are pressings: a department kit, not a place to hide loot.</summary>
    [DataField]
    public bool Writable = true;

    [ViewVariables]
    public int UsedMemory;
}

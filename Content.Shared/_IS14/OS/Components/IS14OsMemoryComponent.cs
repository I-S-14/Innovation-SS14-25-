using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.OS.Components;

/// <summary>
///     Memory is the platform's main constraint (§7). Everything installed is tracked here;
///     the only thing allowed to mutate <see cref="UsedMemory"/> is IS14OsMemorySystem.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsMemoryComponent : Component
{
    /// <summary>Apps installed at map init. System apps are added on top of these automatically.</summary>
    [DataField]
    public List<ProtoId<IS14OsAppPrototype>> Preinstalled = new();

    [ViewVariables]
    public Dictionary<ProtoId<IS14OsAppPrototype>, OsInstallEntry> Installed = new();

    /// <summary>Added by expansion boards.</summary>
    [DataField]
    public int ExtraMemory;

    /// <summary>How many expansion boards are already fitted.</summary>
    [DataField]
    public int UsedSlots;

    [ViewVariables]
    public int UsedMemory;

    /// <summary>Photos, saved notes and anything else the device holds that is not an app.</summary>
    [DataField]
    public List<OsFile> Files = new();

    [DataField]
    public int NextFileId = 1;

    /// <summary>Memory taken by files. Tracked apart from apps so the readout can break it down.</summary>
    [ViewVariables]
    public int UsedFileMemory;
}

[DataDefinition]
public sealed partial class OsInstallEntry
{
    [DataField]
    public OsInstallStatus Status = OsInstallStatus.Ok;

    /// <summary>Memory this install actually reserved, cached so uninstall stays symmetric.</summary>
    [DataField]
    public int Size;

    /// <summary>Preinstalled system software cannot be removed by the user.</summary>
    [DataField]
    public bool Undeletable;
}

public enum OsInstallStatus : byte
{
    Ok,
    Locked,
    Corrupted,
}

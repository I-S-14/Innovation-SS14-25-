using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

[Serializable, NetSerializable]
public sealed class OsFilesState : IS14OsAppState
{
    public List<OsFileMeta> Files = new();

    /// <summary>Bytes of the one file the player currently has open, if any.</summary>
    public OsFilePayload? Open;

    /// <summary>
    ///     The disk in the drive, if there is one. Explorer is where a disk is read, written
    ///     and installed from — a second app for it would be a second place to look (§7.4).
    /// </summary>
    public OsDiskInfo? Disk;

    /// <summary>Loc id of the last refused operation, shown until dismissed.</summary>
    public string? Error;
}

[Serializable, NetSerializable]
public sealed class OsDiskInfo
{
    public string Name = string.Empty;
    public List<ProtoId<IS14OsAppPrototype>> Apps = new();
    public List<OsFileMeta> Files = new();
    public int Used;
    public int Capacity;
    public bool Writable;

    /// <summary>
    ///     Software on the disk this device cannot run at all — stationary programs on a
    ///     handheld, mostly. Listed so the app can say so up front instead of letting the
    ///     player press a button that was never going to work.
    /// </summary>
    public List<ProtoId<IS14OsAppPrototype>> Incompatible = new();
}

/// <summary>Installs one of the disk's applications onto the device.</summary>
[Serializable, NetSerializable]
public sealed class OsDiskInstallEvent : IS14OsAppEvent
{
    public ProtoId<IS14OsAppPrototype> App;

    public OsDiskInstallEvent(ProtoId<IS14OsAppPrototype> app)
    {
        App = app;
    }
}

/// <summary>
///     Moves a file between the device and the disk. Copying, not moving: a courier who hands
///     over the disk should still be able to keep what was on it.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsDiskCopyEvent : IS14OsAppEvent
{
    public int File;

    /// <summary>True copies device to disk, false copies disk to device.</summary>
    public bool ToDisk;

    public OsDiskCopyEvent(int file, bool toDisk)
    {
        File = file;
        ToDisk = toDisk;
    }
}

/// <summary>Erases a file from the disk. Read-only disks refuse.</summary>
[Serializable, NetSerializable]
public sealed class OsDiskDeleteEvent : IS14OsAppEvent
{
    public int File;

    public OsDiskDeleteEvent(int file)
    {
        File = file;
    }
}

[Serializable, NetSerializable]
public sealed class OsFileOpenEvent : IS14OsAppEvent
{
    /// <summary>Null closes whatever is open, which is how the payload stops being sent.</summary>
    public int? File;

    public OsFileOpenEvent(int? file)
    {
        File = file;
    }
}

[Serializable, NetSerializable]
public sealed class OsFileDeleteEvent : IS14OsAppEvent
{
    public int File;

    public OsFileDeleteEvent(int file)
    {
        File = file;
    }
}

[Serializable, NetSerializable]
public sealed class OsFileRenameEvent : IS14OsAppEvent
{
    public int File;
    public string Name;

    public OsFileRenameEvent(int file, string name)
    {
        File = file;
        Name = name;
    }
}

/// <summary>Clears the error line.</summary>
[Serializable, NetSerializable]
public sealed class OsFilesDismissErrorEvent : IS14OsAppEvent
{
}

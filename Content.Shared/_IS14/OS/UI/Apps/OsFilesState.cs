using Content.Shared._IS14.OS.Files;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

[Serializable, NetSerializable]
public sealed class OsFilesState : IS14OsAppState
{
    public List<OsFileMeta> Files = new();

    /// <summary>Bytes of the one file the player currently has open, if any.</summary>
    public OsFilePayload? Open;
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

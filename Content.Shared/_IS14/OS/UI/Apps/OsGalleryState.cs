using Content.Shared._IS14.OS.Files;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     Gallery. Every photo on the device by name, and the bytes of exactly one of them: the
///     client walks the list a picture at a time and keeps what it has already decoded.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsGalleryState : IS14OsAppState
{
    public List<OsFileMeta> Photos = new();

    /// <summary>Bytes of the one photo the client asked for, if it is still there.</summary>
    public OsFilePayload? Photo;
}

[Serializable, NetSerializable]
public sealed class OsGalleryViewEvent : IS14OsAppEvent
{
    /// <summary>Null stops the payload being sent at all.</summary>
    public int? File;

    public OsGalleryViewEvent(int? file)
    {
        File = file;
    }
}

[Serializable, NetSerializable]
public sealed class OsGalleryDeleteEvent : IS14OsAppEvent
{
    public int File;

    public OsGalleryDeleteEvent(int file)
    {
        File = file;
    }
}

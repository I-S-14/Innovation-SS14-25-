using Content.Shared._IS14.OS.Files;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     Messenger. Only the open conversation's messages travel — the contact list carries a
///     one-line preview instead, so a device with forty chats does not push forty transcripts
///     on every update the way a naive port would.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsMessengerState : IS14OsAppState
{
    public string OwnAddress = string.Empty;
    public string OwnName = string.Empty;

    public List<OsChatSummary> Chats = new();

    /// <summary>Everyone on the station running the messenger. Populated only on the directory tab.</summary>
    public List<OsDirectoryEntry>? Directory;

    public string? OpenChat;
    public List<OsChatMessage> Messages = new();

    /// <summary>Files on this device that can be attached. Metadata only — the bytes never
    /// ride along with a chat state.</summary>
    public List<OsFileMeta> Files = new();

    /// <summary>
    ///     Bytes of the one attached photo the client asked for, so it can be drawn inside its
    ///     message bubble. Exactly one payload per update: a transcript full of photos would
    ///     otherwise push a megabyte every time somebody typed.
    /// </summary>
    public OsFilePayload? Photo;

    public bool Muted;
    public string? Error;
}

[Serializable, NetSerializable]
public sealed class OsChatSummary
{
    public string Address = string.Empty;
    public string Name = string.Empty;
    public string? Job;
    public string Preview = string.Empty;
    public bool Unread;
}

[Serializable, NetSerializable]
public sealed class OsDirectoryEntry
{
    public string Address = string.Empty;
    public string Name = string.Empty;
    public string? Job;
}

[Serializable, NetSerializable]
public sealed class OsChatMessage
{
    public bool Outgoing;
    public string Text = string.Empty;
    public TimeSpan Time;

    /// <summary>Metadata of an attached file. The bytes are fetched separately, on demand.</summary>
    public OsFileMeta? Attachment;
}

[Serializable, NetSerializable]
public sealed class OsMessengerOpenChatEvent : IS14OsAppEvent
{
    public string? Address;

    public OsMessengerOpenChatEvent(string? address)
    {
        Address = address;
    }
}

[Serializable, NetSerializable]
public sealed class OsMessengerSendEvent : IS14OsAppEvent
{
    public string Address;
    public string Text;

    /// <summary>Id of a file on the sender's device to send along with the message.</summary>
    public int? Attachment;

    public OsMessengerSendEvent(string address, string text, int? attachment)
    {
        Address = address;
        Text = text;
        Attachment = attachment;
    }
}

/// <summary>
///     Asks for the bytes of one photo attachment in the open chat. The client walks the
///     transcript one photo at a time and stops asking once it has them all.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsMessengerViewPhotoEvent : IS14OsAppEvent
{
    public int? File;

    public OsMessengerViewPhotoEvent(int? file)
    {
        File = file;
    }
}

[Serializable, NetSerializable]
public sealed class OsMessengerDirectoryEvent : IS14OsAppEvent
{
    public bool Show;

    public OsMessengerDirectoryEvent(bool show)
    {
        Show = show;
    }
}

[Serializable, NetSerializable]
public sealed class OsMessengerMuteEvent : IS14OsAppEvent
{
}

[Serializable, NetSerializable]
public sealed class OsMessengerDeleteChatEvent : IS14OsAppEvent
{
    public string Address;

    public OsMessengerDeleteChatEvent(string address)
    {
        Address = address;
    }
}

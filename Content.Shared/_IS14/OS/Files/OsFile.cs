using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.Files;

/// <summary>
///     A file on a device. Payloads stay server-side: only <see cref="OsFileMeta"/> travels with
///     every state push, and the bytes go out one at a time when something is actually opened.
///     A 96 KB photo in every UI update would be indefensible (Docs §4.5).
/// </summary>
[DataDefinition]
public sealed partial class OsFile
{
    [DataField]
    public int Id;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public OsFileKind Kind = OsFileKind.Text;

    /// <summary>Memory taken, in GQ. Charged against the device like an app is.</summary>
    [DataField]
    public int Size = 1;

    [DataField]
    public TimeSpan Created;

    /// <summary>Who or what produced it — the photographer, the note's author.</summary>
    [DataField]
    public string? Author;

    [DataField]
    public string? Text;

    [DataField]
    public byte[]? Data;

    public OsFileMeta ToMeta()
    {
        return new OsFileMeta
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            Size = Size,
            Created = Created,
            Author = Author,
        };
    }
}

[Serializable, NetSerializable]
public sealed class OsFileMeta
{
    public int Id;
    public string Name = string.Empty;
    public OsFileKind Kind;
    public int Size;
    public TimeSpan Created;
    public string? Author;
}

/// <summary>Payload of one opened file.</summary>
[Serializable, NetSerializable]
public sealed class OsFilePayload
{
    public int Id;
    public string? Text;
    public byte[]? Data;
}

[Serializable, NetSerializable]
public enum OsFileKind : byte
{
    Text,
    Photo,
}

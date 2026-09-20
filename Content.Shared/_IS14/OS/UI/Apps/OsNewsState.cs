using Content.Shared.MassMedia.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     Station press. Headlines always, the body of one article only while it is open: a shift
///     of long articles would otherwise ride along with every state push (§4.5).
/// </summary>
[Serializable, NetSerializable]
public sealed class OsNewsState : IS14OsAppState
{
    public List<OsNewsHeadline> Headlines = new();

    /// <summary>Index of the open article in <see cref="Headlines"/>, if one is open.</summary>
    public int? Reading;

    public string? Body;

    /// <summary>Articles published since this device last looked at the list.</summary>
    public int Unread;
}

[Serializable, NetSerializable]
public sealed class OsNewsHeadline
{
    public string Title = string.Empty;
    public string? Author;
    public TimeSpan Published;
}

/// <summary>Opens an article by index, or closes the one open when null.</summary>
[Serializable, NetSerializable]
public sealed class OsNewsReadEvent : IS14OsAppEvent
{
    public int? Article;

    public OsNewsReadEvent(int? article)
    {
        Article = article;
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

[Serializable, NetSerializable]
public sealed class OsNotesState : IS14OsAppState
{
    /// <summary>The document, markup and all — formatting is engine rich-text tags.</summary>
    public string Text;

    /// <summary>What the document is called. Becomes the file name when it is exported.</summary>
    public string Title;

    /// <summary>Loc id of the last export attempt, so saving gives feedback.</summary>
    public string? Status;

    public OsNotesState(string text, string title, string? status = null)
    {
        Text = text;
        Title = title;
        Status = status;
    }
}

/// <summary>
///     Writes the note out as a file, which is what makes it sendable: the messenger attaches
///     files, not app state.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsNotesExportEvent : IS14OsAppEvent
{
    public string Name;

    public OsNotesExportEvent(string name)
    {
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class OsNotesSaveEvent : IS14OsAppEvent
{
    public string Text;
    public string Title;

    public OsNotesSaveEvent(string text, string title)
    {
        Text = text;
        Title = title;
    }
}

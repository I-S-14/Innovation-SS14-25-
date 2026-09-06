using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

[Serializable, NetSerializable]
public sealed class OsNotesState : IS14OsAppState
{
    public string Text;

    /// <summary>Loc id of the last export attempt, so saving gives feedback.</summary>
    public string? Status;

    public OsNotesState(string text, string? status = null)
    {
        Text = text;
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

    public OsNotesSaveEvent(string text)
    {
        Text = text;
    }
}

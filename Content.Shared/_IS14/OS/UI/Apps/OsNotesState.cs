using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

[Serializable, NetSerializable]
public sealed class OsNotesState : IS14OsAppState
{
    public string Text;

    public OsNotesState(string text)
    {
        Text = text;
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

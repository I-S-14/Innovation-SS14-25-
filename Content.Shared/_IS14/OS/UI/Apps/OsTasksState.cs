using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

[Serializable, NetSerializable]
public sealed class OsTasksState : IS14OsAppState
{
    public List<OsTaskEntry> Tasks = new();

    /// <summary>Set when the list is full, so the app can say why nothing was added.</summary>
    public bool Full;
}

[Serializable, NetSerializable]
public sealed class OsTaskEntry
{
    public int Id;
    public string Text = string.Empty;
    public bool Done;
}

[Serializable, NetSerializable]
public sealed class OsTaskAddEvent : IS14OsAppEvent
{
    public string Text;

    public OsTaskAddEvent(string text)
    {
        Text = text;
    }
}

[Serializable, NetSerializable]
public sealed class OsTaskToggleEvent : IS14OsAppEvent
{
    public int Task;

    public OsTaskToggleEvent(int task)
    {
        Task = task;
    }
}

[Serializable, NetSerializable]
public sealed class OsTaskRemoveEvent : IS14OsAppEvent
{
    /// <summary>Null clears everything already ticked off, which is the usual tidy-up.</summary>
    public int? Task;

    public OsTaskRemoveEvent(int? task)
    {
        Task = task;
    }
}

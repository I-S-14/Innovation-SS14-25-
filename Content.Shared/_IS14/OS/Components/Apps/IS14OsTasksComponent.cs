namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     A to-do list on the device. No network, no station, no server of record: the point is a
///     place to write down "check the singulo, then find the HoP" that survives the walk there.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsTasksComponent : Component
{
    [DataField]
    public List<OsTask> Tasks = new();

    [DataField]
    public int NextId = 1;

    /// <summary>
    ///     A cap so the list stays a list. Reached, the oldest finished entry goes; a list that
    ///     refuses to take the thing you just remembered is worse than one that forgets a tick.
    /// </summary>
    [DataField]
    public int MaxTasks = 24;

    [DataField]
    public int MaxLength = 96;
}

[DataDefinition]
public sealed partial class OsTask
{
    [DataField]
    public int Id;

    [DataField]
    public string Text = string.Empty;

    [DataField]
    public bool Done;
}

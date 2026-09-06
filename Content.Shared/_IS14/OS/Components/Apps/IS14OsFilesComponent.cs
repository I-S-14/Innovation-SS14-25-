namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     Browser state for the Files app. Which file is open lives here rather than on the client
///     because the payload is only sent while something is open.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsFilesComponent : Component
{
    [ViewVariables]
    public int? OpenFile;
}

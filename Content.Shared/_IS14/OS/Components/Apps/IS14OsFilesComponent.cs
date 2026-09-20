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

    /// <summary>
    ///     Loc id of the last refused disk operation, shown until dismissed. A button that does
    ///     nothing when pressed is indistinguishable from a broken one, and every reason a disk
    ///     install can fail — wrong kind of device, no room, already installed — is something
    ///     the player can act on once they are told.
    /// </summary>
    [ViewVariables]
    public string? Error;
}

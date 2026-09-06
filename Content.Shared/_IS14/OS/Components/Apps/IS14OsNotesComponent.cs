namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     Data for the Notes app. Added to the device when the app is installed and removed with
///     it — uninstalling really does throw the notes away, and that is the intended cost.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsNotesComponent : Component
{
    [DataField]
    public string Text = string.Empty;

    [DataField]
    public int MaxLength = 4000;

    [ViewVariables]
    public string? Status;
}

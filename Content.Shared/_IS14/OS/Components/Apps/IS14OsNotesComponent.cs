namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     Data for the editor. Added to the device when the app is installed and removed with
///     it — uninstalling really does throw the document away, and that is the intended cost.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsNotesComponent : Component
{
    /// <summary>The document as markup: the toolbar writes engine rich-text tags into it.</summary>
    [DataField]
    public string Text = string.Empty;

    /// <summary>What the document is called. Used as the file name on export.</summary>
    [DataField]
    public string Title = string.Empty;

    [DataField]
    public int MaxLength = 4000;

    [ViewVariables]
    public string? Status;
}

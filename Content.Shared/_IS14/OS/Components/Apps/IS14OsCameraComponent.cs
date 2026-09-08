using Robust.Shared.Audio;

namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>Camera app: photographs what the holder is looking at, straight into the file system.</summary>
[RegisterComponent]
public sealed partial class IS14OsCameraComponent : Component
{
    /// <summary>Hard cap on an accepted frame. Matches the station's own camera hardware.</summary>
    [DataField]
    public int MaxBytes = 96 * 1024;

    [DataField]
    public SoundSpecifier ShutterSound = new SoundPathSpecifier("/Audio/Items/Stamp/automatic_stamp.ogg");

    [ViewVariables]
    public string? Status;
}

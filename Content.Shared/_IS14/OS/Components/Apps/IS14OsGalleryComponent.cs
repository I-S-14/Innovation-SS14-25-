namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     Gallery. It owns no photos of its own — they are ordinary files in the device's memory,
///     the same ones the Files app lists — so uninstalling the viewer costs you nothing but the
///     viewer, and a photo taken before it was installed is still there afterwards.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsGalleryComponent : Component
{
    /// <summary>
    ///     File whose bytes the client has asked for. One at a time: a device holding a dozen
    ///     photos would otherwise push a megabyte of PNG to everyone in PVS on every update.
    /// </summary>
    [ViewVariables]
    public int? Requested;
}

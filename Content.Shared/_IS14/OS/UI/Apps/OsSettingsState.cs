using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     What Settings needs beyond the shell state: which wallpapers this device may use and
///     which of its own photos it could put up instead. Only metadata travels — the picture
///     itself is fetched once, by the shell, when the choice actually changes (§4.5).
/// </summary>
[Serializable, NetSerializable]
public sealed class OsSettingsState : IS14OsAppState
{
    public List<ProtoId<IS14OsWallpaperPrototype>> Wallpapers = new();

    /// <summary>Photos stored on this device, offered as wallpaper candidates.</summary>
    public List<OsFileMeta> Photos = new();
}

/// <summary>
///     Sets the wallpaper. <see cref="Wallpaper"/> is a wallpaper prototype id, or null to
///     fall back to the theme's flat background; <see cref="PhotoId"/> picks a stored photo
///     and wins over the prototype when set.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsSettingsSetWallpaperEvent : IS14OsAppEvent
{
    public string? Wallpaper;
    public int? PhotoId;

    public OsSettingsSetWallpaperEvent(string? wallpaper, int? photoId)
    {
        Wallpaper = wallpaper;
        PhotoId = photoId;
    }
}

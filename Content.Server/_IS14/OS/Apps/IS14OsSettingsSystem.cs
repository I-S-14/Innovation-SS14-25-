using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Server half of Settings. Theme goes through the shell like it always did; this only
///     owns the wallpaper, because picking one means looking at the files the device holds.
/// </summary>
public sealed class IS14OsSettingsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IS14OsFileSystem _files = default!;

    public const string AppId = "AppSettings";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsSettingsComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsSettingsComponent, OsAppEventRaised>(OnAppEvent);
    }

    private void OnGetState(Entity<IS14OsSettingsComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var state = new OsSettingsState();

        foreach (var wallpaper in _proto.EnumeratePrototypes<IS14OsWallpaperPrototype>())
        {
            if (!wallpaper.Unlockable)
                state.Wallpapers.Add(wallpaper.ID);
        }

        if (TryComp(ent, out IS14OsMemoryComponent? memory))
        {
            foreach (var file in memory.Files)
            {
                if (file.Kind == OsFileKind.Photo)
                    state.Photos.Add(file.ToMeta());
            }
        }

        args.State = state;
    }

    private void OnAppEvent(Entity<IS14OsSettingsComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsSettingsSetWallpaperEvent set)
            return;

        if (!TryComp(ent, out IS14OsDeviceComponent? device))
            return;

        // Everything here is client-supplied, so both halves are re-checked: an unlockable
        // wallpaper this device never found, or a photo id it does not hold, are refused.
        if (set.PhotoId is { } photoId)
        {
            if (!TryComp(ent, out IS14OsMemoryComponent? memory))
                return;

            var file = _files.Get(memory, photoId);
            if (file is not { Kind: OsFileKind.Photo })
                return;

            device.Wallpaper = null;
            device.WallpaperPhoto = photoId;
        }
        else if (set.Wallpaper is { } id)
        {
            if (!_proto.TryIndex<IS14OsWallpaperPrototype>(id, out var proto) || proto.Unlockable)
                return;

            device.Wallpaper = id;
            device.WallpaperPhoto = null;
        }
        else
        {
            device.Wallpaper = null;
            device.WallpaperPhoto = null;
        }
    }
}

using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The photo gallery. It is a view onto the file system, not a second store: the pictures
///     are the same files the camera wrote and the messenger delivered, and deleting one here
///     frees the same memory it would in Files.
/// </summary>
public sealed class IS14OsGallerySystem : EntitySystem
{
    public const string AppId = "AppGallery";

    [Dependency] private readonly IS14OsFileSystem _files = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsGalleryComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsGalleryComponent, OsAppEventRaised>(OnAppEvent);
        SubscribeLocalEvent<IS14OsGalleryComponent, OsAppClosedEvent>(OnClosed);
    }

    private void OnGetState(Entity<IS14OsGalleryComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        var state = new OsGalleryState();

        // Newest first: the shot you just took is the one you are looking for.
        for (var i = memory.Files.Count - 1; i >= 0; i--)
        {
            var file = memory.Files[i];
            if (file.Kind == OsFileKind.Photo)
                state.Photos.Add(file.ToMeta());
        }

        if (ent.Comp.Requested is { } id
            && _files.Get(memory, id) is { Kind: OsFileKind.Photo } photo)
        {
            state.Photo = new OsFilePayload
            {
                Id = photo.Id,
                Data = photo.Data,
            };
        }

        args.State = state;
    }

    private void OnAppEvent(Entity<IS14OsGalleryComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        switch (args.Event)
        {
            case OsGalleryViewEvent view:
                ent.Comp.Requested = view.File is { } file && _files.Get(memory, file) != null
                    ? view.File
                    : null;
                break;

            case OsGalleryDeleteEvent delete:
                if (ent.Comp.Requested == delete.File)
                    ent.Comp.Requested = null;

                _files.Remove(memory, delete.File);
                break;
        }
    }

    /// <summary>Closing the app drops the payload: nothing should keep streaming unseen.</summary>
    private void OnClosed(Entity<IS14OsGalleryComponent> ent, ref OsAppClosedEvent args)
    {
        if (args.App == AppId)
            ent.Comp.Requested = null;
    }
}

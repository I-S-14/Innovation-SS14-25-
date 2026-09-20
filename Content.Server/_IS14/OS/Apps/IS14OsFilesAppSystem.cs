using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The Files browser. Lists metadata always, hands over a payload only for the one file the
///     player has open — a gallery of photos would otherwise be pushed to everyone in PVS on
///     every tick the device updates.
/// </summary>
public sealed class IS14OsFilesAppSystem : EntitySystem
{
    public const string AppId = "AppFiles";

    private const int MaxNameLength = 32;

    [Dependency] private readonly IS14OsFileSystem _files = default!;
    [Dependency] private readonly IS14OsDiskSystem _disks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsFilesComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsFilesComponent, OsAppEventRaised>(OnAppEvent);
        SubscribeLocalEvent<IS14OsFilesComponent, OsAppClosedEvent>(OnClosed);
    }

    private void OnGetState(Entity<IS14OsFilesComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        var state = new OsFilesState();

        foreach (var file in memory.Files)
            state.Files.Add(file.ToMeta());

        if (ent.Comp.OpenFile is { } id && _files.Get(memory, id) is { } open)
        {
            state.Open = new OsFilePayload
            {
                Id = open.Id,
                Text = open.Text,
                Data = open.Data,
            };
        }

        if (_disks.GetDisk(ent) is { } disk)
        {
            var info = new OsDiskInfo
            {
                Name = Name(disk.Owner),
                Used = disk.Comp.UsedMemory,
                Capacity = disk.Comp.Capacity,
                Writable = disk.Comp.Writable,
                Apps = new List<ProtoId<IS14OsAppPrototype>>(disk.Comp.Apps),
            };

            foreach (var file in disk.Comp.Files)
                info.Files.Add(file.ToMeta());

            // Software this device could never run is marked here rather than left for the
            // player to discover by pressing a button that quietly refuses.
            if (TryComp(ent, out IS14OsDeviceComponent? flagDevice))
            {
                foreach (var app in info.Apps)
                {
                    if (!_disks.CanRun(flagDevice, app))
                        info.Incompatible.Add(app);
                }
            }

            state.Disk = info;
        }

        state.Error = ent.Comp.Error;
        args.State = state;
    }

    private void OnAppEvent(Entity<IS14OsFilesComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        switch (args.Event)
        {
            case OsFileOpenEvent open:
                ent.Comp.OpenFile = open.File != null && _files.Get(memory, open.File.Value) != null
                    ? open.File
                    : null;
                break;

            case OsFileDeleteEvent delete:
                if (ent.Comp.OpenFile == delete.File)
                    ent.Comp.OpenFile = null;

                _files.Remove(memory, delete.File);
                break;

            case OsDiskInstallEvent install:
                ent.Comp.Error = TryComp(ent, out IS14OsDeviceComponent? installDevice)
                    && _disks.GetDisk(ent) is { } installDisk
                        ? _disks.Install((ent.Owner, installDevice, memory), installDisk, install.App)
                        : "is14-os-disk-error-no-disk";

                break;

            case OsDiskCopyEvent copy:
                if (_disks.GetDisk(ent) is not { } copyDisk)
                {
                    ent.Comp.Error = "is14-os-disk-error-no-disk";
                }
                else if (copy.ToDisk)
                {
                    ent.Comp.Error = _disks.CopyToDisk(memory, copyDisk, copy.File);
                }
                else
                {
                    ent.Comp.Error = TryComp(ent, out IS14OsDeviceComponent? copyDevice)
                        ? _disks.CopyToDevice((ent.Owner, copyDevice, memory), copyDisk, copy.File)
                        : "is14-os-disk-error-no-disk";
                }

                break;

            case OsDiskDeleteEvent diskDelete:
                ent.Comp.Error = _disks.GetDisk(ent) is { } deleteDisk
                    ? _disks.Delete(deleteDisk, diskDelete.File)
                    : "is14-os-disk-error-no-disk";

                break;

            case OsFilesDismissErrorEvent:
                ent.Comp.Error = null;
                break;

            case OsFileRenameEvent rename:
                if (_files.Get(memory, rename.File) is { } file)
                {
                    var name = rename.Name.Trim();
                    if (name.Length > 0)
                        file.Name = name.Length > MaxNameLength ? name[..MaxNameLength] : name;
                }

                break;
        }
    }

    /// <summary>Closing the app drops the payload too; nothing should keep streaming unseen.</summary>
    private void OnClosed(Entity<IS14OsFilesComponent> ent, ref OsAppClosedEvent args)
    {
        if (args.App != AppId)
            return;

        ent.Comp.OpenFile = null;
        ent.Comp.Error = null;
    }
}

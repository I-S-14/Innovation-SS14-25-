using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS;

/// <summary>
///     Disks in and out of the drive, and everything that moves across it (Docs §7.4).
///
///     Explorer is the only front end: a disk is storage, and the place a player already looks
///     for storage is the file browser. What the disk adds is that this particular storage can
///     be pulled out and walked away with.
/// </summary>
public sealed class IS14OsDiskSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;
    [Dependency] private readonly IS14OsMemorySystem _memory = default!;
    [Dependency] private readonly IS14OsFileSystem _files = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsDeviceComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<IS14OsDeviceComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<IS14OsDiskComponent, MapInitEvent>(OnDiskInit);
    }

    /// <summary>Disks authored in YAML come with files already on them; charge for them once.</summary>
    private void OnDiskInit(Entity<IS14OsDiskComponent> ent, ref MapInitEvent args)
    {
        var used = 0;
        var nextId = ent.Comp.NextFileId;

        foreach (var file in ent.Comp.Files)
        {
            used += file.Size;

            if (file.Id >= nextId)
                nextId = file.Id + 1;
        }

        ent.Comp.UsedMemory = used;
        ent.Comp.NextFileId = nextId;
    }

    private void OnInserted(Entity<IS14OsDeviceComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == IS14OsDiskComponent.SlotId)
            _os.UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnRemoved(Entity<IS14OsDeviceComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == IS14OsDiskComponent.SlotId)
            _os.UpdateUi(ent.Owner, ent.Comp);
    }

    /// <summary>The disk currently in the device's drive, if it has a drive at all.</summary>
    public Entity<IS14OsDiskComponent>? GetDisk(EntityUid device)
    {
        if (!_container.TryGetContainer(device, IS14OsDiskComponent.SlotId, out var container))
            return null;

        foreach (var contained in container.ContainedEntities)
        {
            if (TryComp(contained, out IS14OsDiskComponent? disk))
                return (contained, disk);
        }

        return null;
    }

    /// <summary>
    ///     Installs software off the disk, and says why not when it will not. Memory is still
    ///     the limit — a disk is not a loophole — and neither are device flags: the software on
    ///     a command disk runs on a console and nowhere else. The reason is returned rather than
    ///     swallowed because a button that silently does nothing reads as a broken button.
    /// </summary>
    public string? Install(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent,
        Entity<IS14OsDiskComponent> disk,
        ProtoId<IS14OsAppPrototype> app)
    {
        if (!disk.Comp.Apps.Contains(app))
            return "is14-os-disk-error-missing";

        // Source flags say where an app may legitimately come from; a disk-only app is exactly
        // the kind of thing that must not quietly appear in the store, and vice versa.
        if (!_proto.TryIndex(app, out var proto) || (proto.Source & OsAppSource.Disk) == 0)
            return "is14-os-disk-error-missing";

        if (_memory.IsInstalled(ent.Comp2, app))
            return "is14-os-disk-error-installed";

        if ((proto.DeviceFlags & _memory.GetDeviceFlags(ent.Comp1)) == 0)
            return "is14-os-disk-error-device";

        if (proto.Size > _memory.GetFreeMemory((ent.Owner, ent.Comp1, ent.Comp2)))
            return "is14-os-disk-error-memory";

        return _memory.Install((ent.Owner, ent.Comp1, ent.Comp2), app)
            ? null
            : "is14-os-disk-error-refused";
    }

    /// <summary>Whether this device could ever run the app, regardless of free space.</summary>
    public bool CanRun(IS14OsDeviceComponent device, ProtoId<IS14OsAppPrototype> app)
    {
        return _proto.TryIndex(app, out var proto)
               && (proto.DeviceFlags & _memory.GetDeviceFlags(device)) != 0;
    }

    /// <summary>Copies a file from the disk onto the device, or says why it would not fit.</summary>
    public string? CopyToDevice(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent,
        Entity<IS14OsDiskComponent> disk,
        int fileId)
    {
        var file = Find(disk.Comp, fileId);
        if (file == null)
            return "is14-os-disk-error-missing";

        return _files.Copy((ent.Owner, ent.Comp1, ent.Comp2), file) != null
            ? null
            : "is14-os-disk-error-memory";
    }

    /// <summary>Copies a file from the device onto the disk, or says why it would not fit.</summary>
    public string? CopyToDisk(IS14OsMemoryComponent memory, Entity<IS14OsDiskComponent> disk, int fileId)
    {
        if (!disk.Comp.Writable)
            return "is14-os-disk-error-readonly";

        var file = _files.Get(memory, fileId);
        if (file == null)
            return "is14-os-disk-error-missing";

        if (disk.Comp.UsedMemory + file.Size > disk.Comp.Capacity)
            return "is14-os-disk-error-disk-full";

        disk.Comp.Files.Add(new OsFile
        {
            Id = disk.Comp.NextFileId++,
            Name = file.Name,
            Kind = file.Kind,
            Size = file.Size,
            Created = _timing.CurTime,
            Author = file.Author,
            Text = file.Text,
            Data = file.Data,
        });

        disk.Comp.UsedMemory += file.Size;
        return null;
    }

    public string? Delete(Entity<IS14OsDiskComponent> disk, int fileId)
    {
        if (!disk.Comp.Writable)
            return "is14-os-disk-error-readonly";

        var file = Find(disk.Comp, fileId);
        if (file == null)
            return "is14-os-disk-error-missing";

        disk.Comp.Files.Remove(file);
        disk.Comp.UsedMemory = Math.Max(0, disk.Comp.UsedMemory - file.Size);
        return null;
    }

    private static OsFile? Find(IS14OsDiskComponent disk, int id)
    {
        foreach (var file in disk.Files)
        {
            if (file.Id == id)
                return file;
        }

        return null;
    }
}

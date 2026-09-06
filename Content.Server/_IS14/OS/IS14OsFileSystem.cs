using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Files;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS;

/// <summary>
///     Files on a device: photos, saved notes, anything a program produces. They are charged
///     against the same memory pool as applications, so a full gallery really does cost you an
///     app — which is the whole point of the constraint (Docs §7.2).
/// </summary>
public sealed class IS14OsFileSystem : EntitySystem
{
    /// <summary>Bytes per GQ. A full-size photo lands around six, so a few of them hurt.</summary>
    public const int BytesPerUnit = 16 * 1024;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IS14OsMemorySystem _memory = default!;

    public static int SizeOf(int bytes)
    {
        return Math.Max(1, (int) MathF.Ceiling(bytes / (float) BytesPerUnit));
    }

    /// <summary>
    ///     Stores a file if there is room. Returns null when the device is full — the caller is
    ///     expected to tell the player, because silently dropping a photo would be maddening.
    /// </summary>
    public OsFile? TryAdd(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent,
        string name,
        OsFileKind kind,
        int size,
        string? author = null,
        string? text = null,
        byte[]? data = null)
    {
        if (size > _memory.GetFreeMemory(ent))
            return null;

        var file = new OsFile
        {
            Id = ent.Comp2.NextFileId++,
            Name = name,
            Kind = kind,
            Size = size,
            Created = _timing.CurTime,
            Author = author,
            Text = text,
            Data = data,
        };

        ent.Comp2.Files.Add(file);
        ent.Comp2.UsedFileMemory += size;
        ent.Comp2.UsedMemory += size;
        return file;
    }

    public bool Remove(IS14OsMemoryComponent memory, int id)
    {
        var file = Get(memory, id);
        if (file == null)
            return false;

        memory.Files.Remove(file);
        memory.UsedFileMemory = Math.Max(0, memory.UsedFileMemory - file.Size);
        memory.UsedMemory = Math.Max(0, memory.UsedMemory - file.Size);
        return true;
    }

    public OsFile? Get(IS14OsMemoryComponent memory, int id)
    {
        foreach (var file in memory.Files)
        {
            if (file.Id == id)
                return file;
        }

        return null;
    }

    /// <summary>Copies a file onto another device — how an attachment actually arrives.</summary>
    public OsFile? Copy(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> target, OsFile source)
    {
        return TryAdd(target,
            source.Name,
            source.Kind,
            source.Size,
            source.Author,
            source.Text,
            source.Data);
    }
}

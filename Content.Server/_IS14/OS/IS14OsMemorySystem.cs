using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.OS;

/// <summary>
///     Owns everything about installed software and the memory it takes. Memory is the
///     platform's main constraint (Docs/_IS14/os-design.md §7), so exactly one system is
///     allowed to change the numbers.
/// </summary>
public sealed class IS14OsMemorySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    ///     Installs the system partition plus whatever the device prototype asked for.
    ///     Called by IS14OsSystem on map init, before any UI can exist.
    /// </summary>
    public void SetupDevice(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent)
    {
        var flags = GetDeviceFlags(ent.Comp1);

        // System apps land on every compatible device and cannot be removed.
        foreach (var app in _proto.EnumeratePrototypes<IS14OsAppPrototype>())
        {
            if (!app.Undeletable || (app.DeviceFlags & flags) == 0)
                continue;

            Install(ent, app.ID, force: true);
        }

        foreach (var appId in ent.Comp2.Preinstalled)
        {
            Install(ent, appId, force: true);
        }
    }

    public bool IsInstalled(IS14OsMemoryComponent memory, ProtoId<IS14OsAppPrototype> app)
    {
        return memory.Installed.ContainsKey(app);
    }

    /// <summary>
    ///     Recomputes the single used-memory figure from its three parts. Everything that moves
    ///     memory goes through here rather than adjusting the total by hand: an install, a
    ///     photo and a chat message all touch it, and three places nudging one counter is how
    ///     that counter ends up wrong by the end of a shift.
    /// </summary>
    public void Recalculate(IS14OsMemoryComponent memory)
    {
        var apps = 0;
        var data = 0;

        foreach (var entry in memory.Installed.Values)
        {
            apps += entry.Size;
            data += entry.DataUsed;
        }

        memory.UsedDataMemory = data;
        memory.UsedMemory = apps + data + memory.UsedFileMemory;
    }

    /// <summary>
    ///     Reports how much room an app's own data now takes and charges it. The return value
    ///     is what actually fitted: an app that asked for more than its <c>DataCap</c>, or more
    ///     than the device has left, is expected to throw away its oldest data and ask again.
    ///     Nothing ever refuses to store a message — the platform's promise is that the player
    ///     never reaches a dead end, only an older history (Docs §7.3).
    /// </summary>
    public int SetDataUsage(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent,
        ProtoId<IS14OsAppPrototype> appId,
        int units)
    {
        if (!ent.Comp2.Installed.TryGetValue(appId, out var entry))
            return 0;

        var cap = _proto.TryIndex(appId, out var app) ? app.DataCap : 0;
        var allowed = Math.Clamp(units, 0, Math.Max(0, cap));

        // Whatever this app is already charged for is not competing with itself for space.
        var headroom = GetFreeMemory(ent) + entry.DataUsed;
        allowed = Math.Min(allowed, Math.Max(0, headroom));

        entry.DataUsed = allowed;
        Recalculate(ent.Comp2);
        return allowed;
    }

    public int GetDataUsage(IS14OsMemoryComponent memory, ProtoId<IS14OsAppPrototype> appId)
    {
        return memory.Installed.TryGetValue(appId, out var entry) ? entry.DataUsed : 0;
    }

    public int GetTotalMemory(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent)
    {
        var profile = GetProfile(ent.Comp1);
        return (profile?.Memory ?? 0) + ent.Comp2.ExtraMemory;
    }

    public int GetSystemMemory(IS14OsDeviceComponent device)
    {
        return GetProfile(device)?.SystemMemory ?? 0;
    }

    public int GetFreeMemory(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent)
    {
        return GetTotalMemory(ent) - GetSystemMemory(ent.Comp1) - ent.Comp2.UsedMemory;
    }

    /// <summary>
    ///     Installs an app: reserves memory and adds the app's data components to the device.
    ///     <paramref name="force"/> skips the memory check, for the system partition.
    /// </summary>
    public bool Install(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent,
        ProtoId<IS14OsAppPrototype> appId,
        bool force = false)
    {
        if (ent.Comp2.Installed.ContainsKey(appId))
            return false;

        if (!_proto.TryIndex(appId, out var app))
            return false;

        if ((app.DeviceFlags & GetDeviceFlags(ent.Comp1)) == 0)
            return false;

        // System software is accounted for by the profile's SystemMemory, so it reserves nothing.
        var size = app.Undeletable ? 0 : app.Size;

        if (!force && size > GetFreeMemory(ent))
            return false;

        EntityManager.AddComponents(ent.Owner, app.Components, removeExisting: false);

        ent.Comp2.Installed[appId] = new OsInstallEntry
        {
            Size = size,
            Undeletable = app.Undeletable,
        };
        Recalculate(ent.Comp2);
        return true;
    }

    /// <summary>
    ///     Uninstalls an app. Its data components go with it — losing the data is the point.
    /// </summary>
    public bool Uninstall(Entity<IS14OsDeviceComponent, IS14OsMemoryComponent> ent,
        ProtoId<IS14OsAppPrototype> appId)
    {
        if (!ent.Comp2.Installed.TryGetValue(appId, out var entry) || entry.Undeletable)
            return false;

        if (_proto.TryIndex(appId, out var app))
            EntityManager.RemoveComponents(ent.Owner, app.Components);

        ent.Comp2.Installed.Remove(appId);
        Recalculate(ent.Comp2);
        return true;
    }

    public IS14OsProfilePrototype? GetProfile(IS14OsDeviceComponent device)
    {
        return _proto.TryIndex(device.Profile, out var profile) ? profile : null;
    }

    public OsDeviceFlags GetDeviceFlags(IS14OsDeviceComponent device)
    {
        return GetProfile(device)?.FormFactor switch
        {
            OsFormFactor.Portable => OsDeviceFlags.Portable,
            OsFormFactor.Stationary => OsDeviceFlags.Stationary,
            _ => OsDeviceFlags.Handheld,
        };
    }
}

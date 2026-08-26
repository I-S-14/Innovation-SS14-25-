// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Grants a storage grid for as long as the module is installed — to the chassis for
///     the suit's general pockets, or to the module itself for a satchel that has to
///     coexist with them. An entity gets one storage component, so anything that is not
///     "the" pockets has to carry its own.
/// </summary>
public sealed class ModuleStorageSystem : ModuleBehaviourSystem<ModuleStorageComponent>
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    /// <summary>Settings key that opens the compartments.</summary>
    public const string OpenKey = "open";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleStorageComponent, ModuleUsedEvent>(OnUsed);
        SubscribeLocalEvent<ModuleStorageComponent, ModuleGetConfigEvent>(OnGetConfig);
        SubscribeLocalEvent<ModuleStorageComponent, ModuleConfigChangedEvent>(OnConfigChanged);
    }

    /// <summary>
    ///     Opens the compartments. This is the only part of the module the player ever
    ///     interacts with directly.
    /// </summary>
    private void OnUsed(Entity<ModuleStorageComponent> ent, ref ModuleUsedEvent args)
    {
        if (args.User is not { } user)
            return;

        args.Handled = Open(ent, args.Chassis, user);
    }

    /// <summary>
    ///     A storage module whose switch does something else — the ore satchel's magnet,
    ///     say — has no "use" button left to open it with, so the settings carry one.
    /// </summary>
    private void OnGetConfig(Entity<ModuleStorageComponent> ent, ref ModuleGetConfigEvent args)
    {
        args.Entries.Add(new ModuleConfigEntry(
            OpenKey,
            Loc.GetString("chassis-config-open-storage"),
            ModuleConfigKind.Button));
    }

    private void OnConfigChanged(Entity<ModuleStorageComponent> ent, ref ModuleConfigChangedEvent args)
    {
        if (args.Key != OpenKey || GetChassis(ent) is not { } chassis)
            return;

        if (GetChassisUser(chassis) is { } user)
            Open(ent, chassis, user);

        args.Handled = true;
    }

    private bool Open(Entity<ModuleStorageComponent> ent, EntityUid chassis, EntityUid user)
    {
        var host = ent.Comp.Host ?? (ent.Comp.OnChassis ? chassis : ent.Owner);

        if (!HasComp<StorageComponent>(host))
            return false;

        _storage.OpenStorageUI(host, user, silent: false);
        return true;
    }

    protected override void Start(Entity<ModuleStorageComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Applied)
            return;

        // Storage is granted on installation, not on the suit being sealed: a bag you
        // could only reach in vacuum would be a worse item, not a more interesting one.
        var host = ent.Comp.OnChassis ? chassis : ent.Owner;
        ent.Comp.Host = host;

        ent.Comp.Granted = !HasComp<StorageComponent>(host);

        var storage = EnsureComp<StorageComponent>(host);

        ent.Comp.PreviousGrid = new List<Box2i>(storage.Grid);
        ent.Comp.PreviousWhitelist = storage.Whitelist;
        ent.Comp.PreviousMaxItemSize = storage.MaxItemSize;

        storage.Grid = new List<Box2i>(ent.Comp.Grid);
        storage.Whitelist = ent.Comp.Whitelist;

        if (ent.Comp.MaxItemSize != null)
            _storage.SetMaxItemSize((host, storage), ent.Comp.MaxItemSize);

        // The occupancy mask is built once, in ComponentInit, from whatever grid existed
        // then — an empty one, for a component we just added. Without this rebuild every
        // insert is refused for want of space in a grid the storage does not know it has.
        _storage.UpdateOccupied((host, storage));

        // E on the suit reaches for the pockets, which is what a player expects of
        // something worn on the back. The panel readout has moved to alt-interact.
        // A satchel on the module has no such gesture and is opened from the panel.
        storage.OpenOnActivate = ent.Comp.OnChassis;

        ent.Comp.Applied = true;
        Dirty(host, storage);
    }

    protected override void Stop(Entity<ModuleStorageComponent> ent, EntityUid chassis)
    {
        if (!ent.Comp.Applied)
            return;

        ent.Comp.Applied = false;

        var host = ent.Comp.Host ?? chassis;
        ent.Comp.Host = null;

        // A satchel that lives on the module keeps everything when the module is pulled:
        // the bag leaves with the bag. Only the suit's own pockets have to be handed back.
        if (!ent.Comp.OnChassis)
            return;

        // A chassis being deleted takes the compartments and everything in them with it.
        // Reaching into a terminating container only produces errors.
        if (TerminatingOrDeleted(host))
            return;

        if (!TryComp<StorageComponent>(host, out var storage))
            return;

        // Anything still inside would be stranded in a grid that no longer has room for
        // it, so hand it back rather than quietly eating the player's belongings.
        _container.EmptyContainer(storage.Container);

        storage.Grid = ent.Comp.PreviousGrid ?? new List<Box2i>();
        storage.Whitelist = ent.Comp.PreviousWhitelist;
        _storage.SetMaxItemSize((host, storage), ent.Comp.PreviousMaxItemSize);

        ent.Comp.PreviousGrid = null;
        ent.Comp.PreviousWhitelist = null;
        ent.Comp.PreviousMaxItemSize = null;

        _storage.UpdateOccupied((host, storage));

        // Leaving an empty bag behind would keep offering the player a pocket that no
        // longer exists.
        if (ent.Comp.Granted)
        {
            ent.Comp.Granted = false;
            RemComp<StorageComponent>(host);
            return;
        }

        Dirty(host, storage);
    }

    /// <summary>
    ///     Storage follows installation rather than the module's switch.
    /// </summary>
    protected override bool RequiresActive(Entity<ModuleStorageComponent> ent) => false;
}

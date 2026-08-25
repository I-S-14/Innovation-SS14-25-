// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Grants the chassis a storage grid for as long as the module is installed.
/// </summary>
public sealed class ModuleStorageSystem : ModuleBehaviourSystem<ModuleStorageComponent>
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleStorageComponent, ModuleUsedEvent>(OnUsed);
    }

    /// <summary>
    ///     Opens the compartments. The grid lives on the chassis, so this is the only
    ///     part of the module the player ever interacts with directly.
    /// </summary>
    private void OnUsed(Entity<ModuleStorageComponent> ent, ref ModuleUsedEvent args)
    {
        if (args.User is not { } user || !HasComp<StorageComponent>(args.Chassis))
            return;

        _storage.OpenStorageUI(args.Chassis, user, silent: false);
        args.Handled = true;
    }

    protected override void Start(Entity<ModuleStorageComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Applied)
            return;

        // Storage is granted on installation, not on the suit being sealed: a bag you
        // could only reach in vacuum would be a worse item, not a more interesting one.
        ent.Comp.Granted = !HasComp<StorageComponent>(chassis);

        var storage = EnsureComp<StorageComponent>(chassis);

        ent.Comp.PreviousGrid = new List<Box2i>(storage.Grid);
        storage.Grid = new List<Box2i>(ent.Comp.Grid);

        // Clicking the chassis belongs to its own interface; the compartments are opened
        // from the module's button there, or from the context menu.
        storage.OpenOnActivate = false;

        ent.Comp.Applied = true;
        Dirty(chassis, storage);
    }

    protected override void Stop(Entity<ModuleStorageComponent> ent, EntityUid chassis)
    {
        if (!ent.Comp.Applied)
            return;

        ent.Comp.Applied = false;

        if (!TryComp<StorageComponent>(chassis, out var storage))
            return;

        // Anything still inside would be stranded in a grid that no longer has room for
        // it, so hand it back rather than quietly eating the player's belongings.
        _container.EmptyContainer(storage.Container);

        storage.Grid = ent.Comp.PreviousGrid ?? new List<Box2i>();
        ent.Comp.PreviousGrid = null;

        // Leaving an empty bag behind would keep offering the player a pocket that no
        // longer exists.
        if (ent.Comp.Granted)
        {
            ent.Comp.Granted = false;
            RemComp<StorageComponent>(chassis);
            return;
        }

        Dirty(chassis, storage);
    }

    /// <summary>
    ///     Storage follows installation rather than the module's switch.
    /// </summary>
    protected override bool RequiresActive(Entity<ModuleStorageComponent> ent) => false;
}

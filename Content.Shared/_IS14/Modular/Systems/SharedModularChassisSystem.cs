// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Inventory;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using System.Numerics;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Modular.Systems;

/// <summary>
///     Owns the chassis side of the module system: what is installed, whether it fits,
///     and which modules are currently allowed to run.
///     Deliberately free of any clothing or inventory concepts so mechs can reuse it.
/// </summary>
public sealed partial class SharedModularChassisSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedChassisModuleSystem _modules = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularChassisComponent, ComponentInit>(OnChassisInit);
        SubscribeLocalEvent<ModularChassisComponent, MapInitEvent>(OnChassisMapInit);
        SubscribeLocalEvent<ModularChassisComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<ModularChassisComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<ModularChassisComponent, ChassisUserChangedEvent>(OnUserChanged);

        InitializeInteraction();
    }

    /// <summary>
    ///     Fans the operator change out to every installed module.
    /// </summary>
    private void OnUserChanged(Entity<ModularChassisComponent> ent, ref ChassisUserChangedEvent args)
    {
        var ev = new ModuleUserChangedEvent(ent, args.User);

        foreach (var module in GetModuleEntities(ent))
        {
            RaiseLocalEvent(module, ref ev);
        }
    }

    private void OnChassisInit(Entity<ModularChassisComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ModuleContainer = _container.EnsureContainer<Container>(ent, ent.Comp.ModuleContainerId);
    }

    /// <summary>
    ///     The module container, resolved rather than trusted. On the client a container
    ///     state can arrive before <see cref="ComponentInit"/> has populated the field,
    ///     so reading it blindly throws while applying entity state.
    /// </summary>
    private bool TryGetModuleContainer(Entity<ModularChassisComponent> ent, [NotNullWhen(true)] out Container? container)
    {
        if (ent.Comp.ModuleContainer != null)
        {
            container = ent.Comp.ModuleContainer;
            return true;
        }

        if (_container.TryGetContainer(ent, ent.Comp.ModuleContainerId, out var found)
            && found is Container moduleContainer)
        {
            ent.Comp.ModuleContainer = moduleContainer;
            container = moduleContainer;
            return true;
        }

        container = null;
        return false;
    }

    /// <summary>
    ///     Installed modules, or nothing at all if the container is not ready yet.
    /// </summary>
    public IReadOnlyList<EntityUid> GetModuleEntities(Entity<ModularChassisComponent> ent)
    {
        return TryGetModuleContainer(ent, out var container)
            ? container.ContainedEntities
            : Array.Empty<EntityUid>();
    }

    private void OnChassisMapInit(Entity<ModularChassisComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.IntegratedModulesSpawned || ent.Comp.IntegratedModules.Count == 0)
            return;

        // Spawning is server-authoritative; the client will be told about the contents.
        if (_net.IsClient)
            return;

        ent.Comp.IntegratedModulesSpawned = true;

        // Relative to the chassis, not nullspace: a nullspace spawn never runs
        // MapInit, and modules that build their hardware there would come up empty.
        var coords = new EntityCoordinates(ent, Vector2.Zero);

        foreach (var proto in ent.Comp.IntegratedModules)
        {
            var module = Spawn(proto, coords);

            if (!TryComp<ChassisModuleComponent>(module, out var moduleComp))
            {
                Log.Error($"Integrated module {proto} on {ToPrettyString(ent)} has no ChassisModuleComponent.");
                Del(module);
                continue;
            }

            // Built-in gear is part of the chassis, not part of the player's budget.
            moduleComp.Removable = false;
            moduleComp.Complexity = 0;

            if (!TryGetModuleContainer(ent, out var container) || !_container.Insert(module, container))
            {
                Log.Error($"Failed to insert integrated module {proto} into {ToPrettyString(ent)}.");
                Del(module);
            }
        }

        Dirty(ent);
    }

    #region Install / uninstall

    /// <summary>
    ///     Checks every rule that governs whether a module may be installed.
    ///     <paramref name="reason"/> receives a localised explanation on failure.
    /// </summary>
    public bool CanInstall(
        Entity<ModularChassisComponent> chassis,
        Entity<ChassisModuleComponent> module,
        out string? reason)
    {
        reason = null;

        if (!chassis.Comp.PanelOpen)
        {
            reason = Loc.GetString("chassis-panel-closed");
            return false;
        }

        if (chassis.Comp.UsedComplexity + module.Comp.Complexity > chassis.Comp.MaxComplexity)
        {
            reason = Loc.GetString("chassis-complexity-exceeded");
            return false;
        }

        if (TryGetConflict(chassis, module, out var conflicting))
        {
            reason = Loc.GetString("chassis-module-conflict", ("module", Name(conflicting.Value)));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Finds an installed module that this one cannot coexist with.
    ///     Conflicts are symmetric: either side declaring the other's tag blocks the pair.
    /// </summary>
    private bool TryGetConflict(
        Entity<ModularChassisComponent> chassis,
        Entity<ChassisModuleComponent> module,
        [NotNullWhen(true)] out EntityUid? conflicting)
    {
        conflicting = null;

        foreach (var installed in GetModuleEntities(chassis))
        {
            if (!TryComp<ChassisModuleComponent>(installed, out var installedComp))
                continue;

            foreach (var tag in module.Comp.Conflicts)
            {
                if (_tag.HasTag(installed, tag))
                {
                    conflicting = installed;
                    return true;
                }
            }

            foreach (var tag in installedComp.Conflicts)
            {
                if (_tag.HasTag(module.Owner, tag))
                {
                    conflicting = installed;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Installs a module, reporting failure to <paramref name="user"/> if there is one.
    /// </summary>
    public bool TryInstall(
        Entity<ModularChassisComponent> chassis,
        Entity<ChassisModuleComponent> module,
        EntityUid? user = null)
    {
        if (!CanInstall(chassis, module, out var reason))
        {
            if (user != null)
            {
                _popup.PopupClient(reason, chassis, user.Value);
                _audio.PlayPredicted(chassis.Comp.FailSound, chassis, user);
            }

            return false;
        }

        var attempt = new ChassisInstallModuleAttemptEvent(module, user, false);
        RaiseLocalEvent(chassis, ref attempt);
        if (attempt.Cancelled)
            return false;

        if (!TryGetModuleContainer(chassis, out var container) || !_container.Insert(module.Owner, container))
            return false;

        if (user != null)
        {
            _popup.PopupClient(Loc.GetString("chassis-module-installed", ("module", Name(module))), chassis, user.Value);
            _audio.PlayPredicted(chassis.Comp.InstallSound, chassis, user);
        }

        return true;
    }

    /// <summary>
    ///     Removes a module and drops it at the chassis' position.
    /// </summary>
    /// <summary>
    ///     Whether a module would come out if asked, and what to say if not.
    ///
    ///     Split out so the interface can ask the same question it will later act on:
    ///     a button that looks pressable and then refuses is worse than one that is
    ///     visibly dead and says why on hover.
    /// </summary>
    public bool CanUninstall(
        Entity<ModularChassisComponent> chassis,
        Entity<ChassisModuleComponent> module,
        EntityUid? user,
        out string? reason)
    {
        reason = null;

        if (!module.Comp.Removable)
        {
            reason = "chassis-module-not-removable";
            return false;
        }

        if (!chassis.Comp.PanelOpen)
        {
            reason = "chassis-panel-closed";
            return false;
        }

        var attempt = new ChassisUninstallModuleAttemptEvent(chassis, user, false);
        RaiseLocalEvent(module, ref attempt);

        if (!attempt.Cancelled)
            return true;

        reason = attempt.Reason;
        return false;
    }

    public bool TryUninstall(
        Entity<ModularChassisComponent> chassis,
        Entity<ChassisModuleComponent> module,
        EntityUid? user = null)
    {
        if (!CanUninstall(chassis, module, user, out var reason))
        {
            if (user != null)
            {
                if (reason != null)
                    _popup.PopupClient(Loc.GetString(reason), chassis, user.Value);

                _audio.PlayPredicted(chassis.Comp.FailSound, chassis, user);
            }

            return false;
        }

        if (!TryGetModuleContainer(chassis, out var container) || !_container.Remove(module.Owner, container))
            return false;

        // A module pulled from the panel belongs in the hands of whoever is wearing the
        // thing, not on the floor behind them. PickupOrDrop already falls through to the
        // floor when there is no free hand.
        if ((GetOperator(chassis) ?? user) is { } recipient)
            _hands.PickupOrDrop(recipient, module.Owner);

        if (user != null)
        {
            _popup.PopupClient(Loc.GetString("chassis-module-removed", ("module", Name(module))), chassis, user.Value);
            _audio.PlayPredicted(chassis.Comp.RemoveSound, chassis, user);
        }

        return true;
    }

    private void OnModuleInserted(Entity<ModularChassisComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ModuleContainerId)
            return;

        if (!TryComp<ChassisModuleComponent>(args.Entity, out var module))
            return;

        module.Chassis = ent;
        Dirty(args.Entity, module);

        RecalculateComplexity(ent);

        var installed = new ModuleInstalledEvent(ent);
        RaiseLocalEvent(args.Entity, ref installed);

        RefreshModules(ent);
    }

    private void OnModuleRemoved(Entity<ModularChassisComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ModuleContainerId)
            return;

        if (!TryComp<ChassisModuleComponent>(args.Entity, out var module))
            return;

        // Make sure the module is not left running once it is out of the chassis.
        _modules.SetEnabled((args.Entity, module), ent, false);

        var uninstalled = new ModuleUninstalledEvent(ent);
        RaiseLocalEvent(args.Entity, ref uninstalled);

        module.Chassis = null;
        Dirty(args.Entity, module);

        if (ent.Comp.SelectedModule == args.Entity)
        {
            ent.Comp.SelectedModule = null;
            Dirty(ent);
        }

        RecalculateComplexity(ent);
        RefreshModules(ent);
    }

    private void RecalculateComplexity(Entity<ModularChassisComponent> ent)
    {
        var total = 0;

        foreach (var module in GetModuleEntities(ent))
        {
            if (TryComp<ChassisModuleComponent>(module, out var comp))
                total += comp.Complexity;
        }

        if (total == ent.Comp.UsedComplexity)
            return;

        ent.Comp.UsedComplexity = total;
        Dirty(ent);
    }

    #endregion

    #region State

    /// <summary>
    ///     Switches the chassis on or off and re-evaluates every module.
    /// </summary>
    public void SetActive(Entity<ModularChassisComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent);

        var ev = new ChassisStateChangedEvent(active);
        RaiseLocalEvent(ent, ref ev);

        RefreshModules(ent);
    }

    public void SetPanelOpen(Entity<ModularChassisComponent> ent, bool open)
    {
        if (ent.Comp.PanelOpen == open)
            return;

        ent.Comp.PanelOpen = open;
        Dirty(ent);

        var ev = new ChassisPanelChangedEvent(open);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    ///     Whoever is operating this chassis — the wearer of a suit, the pilot of a mech.
    /// </summary>
    public EntityUid? GetOperator(EntityUid chassis)
    {
        var ev = new ChassisGetUserEvent(null);
        RaiseLocalEvent(chassis, ref ev);

        return ev.User;
    }

    /// <summary>
    ///     Slots the chassis currently offers to its modules. A chassis that does not
    ///     answer the query is treated as offering everything, which is what a mech wants.
    /// </summary>
    public SlotFlags GetAvailableSlots(EntityUid chassis)
    {
        var ev = new ChassisGetAvailableSlotsEvent(SlotFlags.NONE, false);
        RaiseLocalEvent(chassis, ref ev);

        return ev.Handled ? ev.Slots : SlotFlags.All;
    }

    /// <summary>
    ///     Re-evaluates whether each installed module may run right now.
    ///     Cheap enough to call whenever anything relevant changes; it only fires
    ///     events for modules whose state actually flipped.
    /// </summary>
    public void RefreshModules(Entity<ModularChassisComponent> ent)
    {
        var available = GetAvailableSlots(ent);

        foreach (var module in GetModuleEntities(ent))
        {
            if (!TryComp<ChassisModuleComponent>(module, out var comp))
                continue;

            var shouldRun = ent.Comp.Active || (comp.Allow & ModuleAllowFlags.ChassisInactive) != 0;
            shouldRun &= _modules.HasRequiredSlots(comp, available);

            _modules.SetEnabled((module, comp), ent, shouldRun);
        }

        var changed = new ChassisModulesChangedEvent();
        RaiseLocalEvent(ent, ref changed);
    }

    /// <summary>
    ///     Enumerates installed modules together with their component.
    /// </summary>
    public IEnumerable<Entity<ChassisModuleComponent>> GetModules(Entity<ModularChassisComponent> ent)
    {
        foreach (var module in GetModuleEntities(ent))
        {
            if (TryComp<ChassisModuleComponent>(module, out var comp))
                yield return (module, comp);
        }
    }

    #endregion
}

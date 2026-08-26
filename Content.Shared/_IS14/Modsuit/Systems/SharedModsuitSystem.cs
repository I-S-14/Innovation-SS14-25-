// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using System.Numerics;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     The MOD suit itself: parts, deployment, sealing and the three-state model.
///     Module handling lives one layer down in <see cref="SharedModularChassisSystem"/>,
///     which knows nothing about clothing.
/// </summary>
public sealed partial class SharedModsuitSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly SharedModularChassisSystem _chassis = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedChassisModuleSystem _modules = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly ModCoreSystem _core = default!;
    [Dependency] private readonly ModsuitVisualsSystem _visuals = default!;
    [Dependency] private readonly SharedModsuitLockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitControlComponent, ComponentInit>(OnControlInit);
        SubscribeLocalEvent<ModsuitControlComponent, MapInitEvent>(OnControlMapInit);
        SubscribeLocalEvent<ModsuitControlComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ModsuitControlComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ModsuitControlComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<ModsuitControlComponent, ActivateInWorldEvent>(OnActivate);

        // The chassis asks which slots it can offer modules; only sealed parts count.
        SubscribeLocalEvent<ModsuitControlComponent, ChassisGetAvailableSlotsEvent>(OnGetAvailableSlots);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisPowerDepletedEvent>(OnPowerDepleted);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisGetUserEvent>(OnGetUser);
        SubscribeLocalEvent<ModsuitControlComponent, EntityTerminatingEvent>(OnTerminating);

        SubscribeLocalEvent<ModsuitControlComponent, ModsuitSealDoAfterEvent>(OnSealDoAfter);

        InitializeParts();
        InitializeIntegrity();
        InitializeRepair();
        InitializeActions();
        InitializeUi();
        InitializeBreach();
    }

    private void OnControlInit(Entity<ModsuitControlComponent> ent, ref ComponentInit args)
    {
        ent.Comp.PartContainer = _container.EnsureContainer<Container>(ent, ent.Comp.PartContainerId);
    }

    /// <summary>
    ///     The part container, resolved rather than trusted: on the client a container
    ///     state can land before <see cref="ComponentInit"/> has filled the field in.
    /// </summary>
    private bool TryGetPartContainer(Entity<ModsuitControlComponent> ent, [NotNullWhen(true)] out Container? container)
    {
        if (ent.Comp.PartContainer != null)
        {
            container = ent.Comp.PartContainer;
            return true;
        }

        if (_container.TryGetContainer(ent, ent.Comp.PartContainerId, out var found)
            && found is Container partContainer)
        {
            ent.Comp.PartContainer = partContainer;
            container = partContainer;
            return true;
        }

        container = null;
        return false;
    }

    private void OnControlMapInit(Entity<ModsuitControlComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.PartsSpawned || ent.Comp.PartPrototypes.Count == 0)
            return;

        if (_net.IsClient)
            return;

        ent.Comp.PartsSpawned = true;

        // See the chassis system: nullspace spawns skip MapInit.
        var coords = new EntityCoordinates(ent, Vector2.Zero);

        foreach (var (slot, proto) in ent.Comp.PartPrototypes)
        {
            var part = Spawn(proto, coords);

            if (!TryComp<ModsuitPartComponent>(part, out var partComp))
            {
                Log.Error($"Modsuit part {proto} on {ToPrettyString(ent)} has no ModsuitPartComponent.");
                Del(part);
                continue;
            }

            partComp.Control = ent;
            partComp.Slot = slot;
            Dirty(part, partComp);

            if (!TryGetPartContainer(ent, out var container) || !_container.Insert(part, container))
            {
                Log.Error($"Failed to insert modsuit part {proto} into {ToPrettyString(ent)}.");
                Del(part);
                continue;
            }

            ent.Comp.Parts[slot] = part;
        }

        Dirty(ent);
    }

    /// <summary>
    ///     E on the suit reaches for the pockets, the way it does on any other back item.
    ///     With no storage module there are none, and the panel readout now lives on
    ///     alt-interact — so say that once rather than leaving the key dead.
    /// </summary>
    private void OnActivate(Entity<ModsuitControlComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || HasComp<StorageComponent>(ent))
            return;

        _popup.PopupClient(Loc.GetString("modsuit-no-storage"), ent, args.User);
    }

    #region Wearer

    private void OnEquipped(Entity<ModsuitControlComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.RequiredSlot) == SlotFlags.NONE)
            return;

        SetWearer(ent, args.Equipee);
    }

    private void OnUnequipped(Entity<ModsuitControlComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.RequiredSlot) == SlotFlags.NONE)
            return;

        SetWearer(ent, null);
    }

    /// <summary>
    ///     A suit with parts still on the body cannot be taken off — they have to be
    ///     retracted first, which is what makes an active suit a commitment.
    /// </summary>
    private void OnUnequipAttempt(Entity<ModsuitControlComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if ((args.SlotFlags & ent.Comp.RequiredSlot) == SlotFlags.NONE)
            return;

        if (!AnyPartDeployed(ent))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("modsuit-parts-still-deployed"), ent, args.Unequipee);
    }

    /// <summary>
    ///     A suit destroyed while worn — gibbed, exploded, admin-deleted — leaves its
    ///     deployed parts on the body. Contained parts die with the suit on their own;
    ///     these are the ones that would be orphaned.
    /// </summary>
    private void OnTerminating(Entity<ModsuitControlComponent> ent, ref EntityTerminatingEvent args)
    {
        // Deleting networked entities is the server's job; the client will be told.
        if (!_net.IsServer)
            return;

        foreach (var part in ent.Comp.Parts.Values)
        {
            if (!TerminatingOrDeleted(part))
                QueueDel(part);
        }
    }

    private void SetWearer(Entity<ModsuitControlComponent> ent, EntityUid? wearer)
    {
        if (ent.Comp.Wearer == wearer)
            return;

        // Never leave parts attached to someone who is no longer wearing the suit —
        // unless the suit itself is dying, in which case OnTerminating cleans up and
        // folding parts back into a terminating container would just error.
        if (wearer == null && !TerminatingOrDeleted(ent))
            RetractAll(ent, silent: true);

        if (ent.Comp.Wearer is { } previous && !TerminatingOrDeleted(previous))
            RemComp<ModsuitWearerComponent>(previous);

        ent.Comp.Wearer = wearer;
        Dirty(ent);

        // Tools have to be usable on the person, because the suit on their back cannot be
        // clicked. See SharedModsuitSystem.Breach.
        if (wearer is { } worn)
        {
            EnsureComp<ModsuitWearerComponent>(worn, out var marker);
            marker.Suit = ent;
            Dirty(worn, marker);
        }

        var ev = new ModsuitWearerChangedEvent(wearer);
        RaiseLocalEvent(ent, ref ev);

        var userChanged = new ChassisUserChangedEvent(wearer);
        RaiseLocalEvent(ent, ref userChanged);

        if (TryComp<ModularChassisComponent>(ent, out var chassis))
            _chassis.RefreshModules((ent, chassis));
    }

    #endregion

    #region Chassis integration

    /// <summary>
    ///     Only sealed, intact parts count. A module that needs a helmet stops working the
    ///     moment the helmet is unsealed — or beaten in — which is the whole point of
    ///     partial sealing and of plating condition.
    /// </summary>
    private void OnGetAvailableSlots(Entity<ModsuitControlComponent> ent, ref ChassisGetAvailableSlotsEvent args)
    {
        var slots = SlotFlags.NONE;

        foreach (var part in ent.Comp.Parts.Values)
        {
            if (!TryComp<ModsuitPartComponent>(part, out var comp) || !comp.Sealed)
                continue;

            // Plating past its break threshold still seals; the hardpoint inside it
            // does not, so anything bolted to that piece goes offline with it.
            if (IsPartBroken((part, comp)))
                continue;

            slots |= comp.SlotFlag;
        }

        args.Slots = slots;
        args.Handled = true;
    }

    private void OnGetUser(Entity<ModsuitControlComponent> ent, ref ChassisGetUserEvent args)
    {
        args.User = ent.Comp.Wearer;
    }

    private void OnPowerDepleted(Entity<ModsuitControlComponent> ent, ref ChassisPowerDepletedEvent args)
    {
        if (ent.Comp.Wearer is { } wearer)
            _popup.PopupClient(Loc.GetString("modsuit-power-depleted"), ent, wearer);

        Deactivate(ent);
    }

    #endregion
}

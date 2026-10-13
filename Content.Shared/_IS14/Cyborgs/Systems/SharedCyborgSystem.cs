// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Cyborgs.Systems;

/// <summary>
/// Core logic of tgstation-style cyborgs: brain, power, activation, model, skin and tools.
/// </summary>
public abstract partial class SharedCyborgSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly EmagSystem _emag = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeBrain();
        InitializeModel();
        InitializeTools();
        InitializeUpgrades();

        SubscribeLocalEvent<TryGetIdentityShortInfoEvent>(OnTryGetIdentityShortInfo);
        SubscribeLocalEvent<CyborgComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CyborgComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CyborgComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CyborgComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CyborgComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<CyborgComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<CyborgComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CyborgComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
        SubscribeLocalEvent<CyborgComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<CyborgComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<CyborgComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<CyborgComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<CyborgComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<CyborgComponent, BeingGibbedEvent>(OnBeingGibbed);
        SubscribeLocalEvent<CyborgComponent, GetCharactedDeadIcEvent>(OnGetDeadIc);
        SubscribeLocalEvent<CyborgComponent, GetCharacterUnrevivableIcEvent>(OnGetUnrevivableIc);
    }

    // Cyborgs go by their name instead of a job title.
    private void OnTryGetIdentityShortInfo(TryGetIdentityShortInfoEvent args)
    {
        if (args.Handled || !HasComp<CyborgComponent>(args.ForActor))
            return;

        args.Title = Name(args.ForActor).Trim();
        args.Handled = true;
    }

    private void OnStartup(Entity<CyborgComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.BrainContainer = _container.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.BrainContainerId);
        ent.Comp.ToolContainer = _container.EnsureContainer<Container>(ent.Owner, ent.Comp.ToolContainerId);
        ent.Comp.UpgradeContainer = _container.EnsureContainer<Container>(ent.Owner, ent.Comp.UpgradeContainerId);
    }

    private void OnMapInit(Entity<CyborgComponent> ent, ref MapInitEvent args)
    {
        for (var i = 0; i < ent.Comp.ToolSlots; i++)
        {
            var hand = GetToolHandId(ent.Comp, i);
            if (!_hands.EnumerateHands(ent.Owner).Contains(hand))
                _hands.AddHand(ent.Owner, hand, HandLocation.Middle);
        }


        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        if (_net.IsServer && ent.Comp.Model is { } model)
            ApplyModel(ent, model, force: true);
    }

    protected virtual void OnInserted(Entity<CyborgComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return; // The changes are already networked with the same game state

        if (args.Container.ID == ent.Comp.BrainContainerId)
            OnBrainInserted(ent, args.Entity);
    }

    protected virtual void OnRemoved(Entity<CyborgComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return; // The changes are already networked with the same game state

        if (args.Container.ID == ent.Comp.BrainContainerId)
            OnBrainRemoved(ent, args.Entity);
    }

    private void OnMindAdded(Entity<CyborgComponent> ent, ref MindAddedMessage args)
    {
        // Unpredicted because the event is raised on the server.
        _popup.PopupEntity(Loc.GetString("borg-mind-added", ("name", Identity.Name(ent.Owner, EntityManager))), ent.Owner);

        TryActivate(ent);

        // Needs a player so that scientists can't drag around an empty cyborg for free all access.
        _access.SetAccessEnabled(ent.Owner, true);
        _appearance.SetData(ent.Owner, CyborgVisuals.HasMind, true);

        if (ent.Comp.Model == null)
            OpenModelMenu(ent);
        else if (!ent.Comp.SkinChosen && _proto.TryIndex(ent.Comp.Model, out var model) && model.Skins.Count > 1)
            OpenSkinMenu(ent); // A cyborg spawned with its model still gets to pick how it looks.
    }

    private void OnMindRemoved(Entity<CyborgComponent> ent, ref MindRemovedMessage args)
    {
        // Unpredicted because the event is raised on the server.
        _popup.PopupEntity(Loc.GetString("borg-mind-removed", ("name", Identity.Name(ent.Owner, EntityManager))), ent.Owner);

        SetActive(ent, false);

        // Turn off the light so that the no-player visuals can be seen.
        if (TryComp<HandheldLightComponent>(ent.Owner, out var light))
            _handheldLight.TurnOff((ent.Owner, light), makeNoise: false);

        _access.SetAccessEnabled(ent.Owner, false);
        _appearance.SetData(ent.Owner, CyborgVisuals.HasMind, false);
    }

    private void OnMobStateChanged(Entity<CyborgComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            TryActivate(ent, args.Origin);
        else
            SetActive(ent, false, args.Origin);
    }

    private void OnPowerCellSlotEmpty(Entity<CyborgComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        SetActive(ent, false);
    }

    private void OnPowerCellChanged(Entity<CyborgComponent> ent, ref PowerCellChangedEvent args)
    {
        TryActivate(ent);
    }

    // The cell can only be swapped by someone else, with the panel open.
    private void OnItemSlotInsertAttempt(Entity<CyborgComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (!args.Cancelled && IsCellSlotBlocked(ent, args.Slot, args.User))
            args.Cancelled = true;
    }

    private void OnItemSlotEjectAttempt(Entity<CyborgComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (!args.Cancelled && IsCellSlotBlocked(ent, args.Slot, args.User))
            args.Cancelled = true;
    }

    private bool IsCellSlotBlocked(Entity<CyborgComponent> ent, ItemSlot slot, EntityUid? user)
    {
        if (!TryComp<PowerCellSlotComponent>(ent, out var cellSlot) ||
            !TryComp<WiresPanelComponent>(ent, out var panel))
            return false;

        if (!_itemSlots.TryGetSlot(ent.Owner, cellSlot.CellSlotId, out var slotToCheck) || slotToCheck != slot)
            return false;

        return !panel.Open || user == ent.Owner;
    }

    // Without power the cyborg crawls at walking speed.
    private void OnRefreshMovementSpeed(Entity<CyborgComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active)
            return;

        if (!TryComp<MovementSpeedModifierComponent>(ent, out var movement) || movement.BaseSprintSpeed == 0f)
            return;

        args.ModifySpeed(1f, movement.BaseWalkSpeed / movement.BaseSprintSpeed);
    }

    private void OnUiOpenAttempt(Entity<CyborgComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.User == ent.Owner && !ent.Comp.CanOpenSelfUi)
            args.Cancel();
    }

    private void OnBeingGibbed(Entity<CyborgComponent> ent, ref BeingGibbedEvent args)
    {
        // Eject the battery even if the slot is locked, and without the ejection sound.
        if (TryComp<PowerCellSlotComponent>(ent, out var cellSlot) &&
            _container.TryGetContainer(ent, cellSlot.CellSlotId, out var cellContainer))
            _container.EmptyContainer(cellContainer);

        _container.EmptyContainer(ent.Comp.BrainContainer);

        // Built-in tools don't survive the chassis.
        DeleteAllTools(ent);
    }

    private void OnGetDeadIc(Entity<CyborgComponent> ent, ref GetCharactedDeadIcEvent args)
    {
        args.Dead = true;
    }

    private void OnGetUnrevivableIc(Entity<CyborgComponent> ent, ref GetCharacterUnrevivableIcEvent args)
    {
        args.Unrevivable = true;
    }

    /// <summary>
    /// Whether the cyborg has power, a player and is alive.
    /// </summary>
    public bool CanActivate(Entity<CyborgComponent> ent)
    {
        if (!_powerCell.HasDrawCharge(ent.Owner))
            return false;

        // Only the client's own mind is networked to it, so this is false for other players' cyborgs on the client.
        if (!_mind.TryGetMind(ent.Owner, out _, out _))
            return false;

        return !_mobState.IsIncapacitated(ent.Owner);
    }

    public bool TryActivate(Entity<CyborgComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.Active || !CanActivate(ent))
            return false;

        SetActive(ent, true, user);
        return true;
    }

    /// <summary>
    /// An active cyborg can use its tools and moves at full speed.
    /// </summary>
    public void SetActive(Entity<CyborgComponent> ent, bool active, EntityUid? user = null)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent);

        if (!active)
            StoreAllTools(ent);

        _powerCell.SetDrawEnabled(ent.Owner, active);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        var sound = active ? ent.Comp.ActivateSound : ent.Comp.DeactivateSound;
        if (user != null)
            _audio.PlayPredicted(sound, ent.Owner, user);
        else if (_net.IsServer)
            _audio.PlayPvs(sound, ent.Owner);
    }

    /// <summary>
    /// Checks the job ban list. Always true on the client.
    /// </summary>
    public virtual bool CanPlayerBeBorged(ICommonSession session)
    {
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CyborgComponent>();
        while (query.MoveNext(out var uid, out var cyborg))
        {
            if (curTime < cyborg.NextBatteryUpdate)
                continue;

            cyborg.NextBatteryUpdate = curTime + TimeSpan.FromSeconds(1);

            // Wake up again once the cell has enough charge.
            TryActivate((uid, cyborg));
        }
    }
}

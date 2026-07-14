using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.Atm;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.Atm;

public sealed class IS14AtmSystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14AtmComponent, EntInsertedIntoContainerMessage>(OnCardSlotChanged);
        SubscribeLocalEvent<IS14AtmComponent, EntRemovedFromContainerMessage>(OnCardSlotChanged);
        SubscribeLocalEvent<IS14AtmComponent, InteractUsingEvent>(OnInteractUsing);

        Subs.BuiEvents<IS14AtmComponent>(IS14AtmUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<IS14AtmSetPinMessage>(OnSetPin);
            subs.Event<IS14AtmEnterPinMessage>(OnEnterPin);
            subs.Event<IS14AtmChangePinMessage>(OnChangePin);
            subs.Event<IS14AtmWithdrawMessage>(OnWithdraw);
            subs.Event<IS14AtmTransferMessage>(OnTransfer);
            subs.Event<IS14AtmEjectCardMessage>(OnEjectCard);
        });
    }

    // ── Card slot / session ───────────────────────────────────────────────────

    private void OnCardSlotChanged(Entity<IS14AtmComponent> atm, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != atm.Comp.CardSlotId)
            return;

        atm.Comp.Authenticated = false;
        UpdateUiState(atm);
    }

    private void OnCardSlotChanged(Entity<IS14AtmComponent> atm, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != atm.Comp.CardSlotId)
            return;

        EndSession(atm);
        UpdateUiState(atm);
    }

    private void OnUiOpened(Entity<IS14AtmComponent> atm, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(atm);
    }

    private void OnUiClosed(Entity<IS14AtmComponent> atm, ref BoundUIClosedEvent args)
    {
        // Walking away ends the session: the next user has to enter the PIN again.
        EndSession(atm);
    }

    private void OnEjectCard(Entity<IS14AtmComponent> atm, ref IS14AtmEjectCardMessage args)
    {
        EndSession(atm);
        EjectCardToHands(atm, args.Actor);
        UpdateUiState(atm);
    }

    // ── PIN ───────────────────────────────────────────────────────────────────

    private void OnSetPin(Entity<IS14AtmComponent> atm, ref IS14AtmSetPinMessage args)
    {
        var account = GetInsertedAccount(atm, out _);
        if (account == null || account.PinSet || IsLocked(account))
            return;

        if (!TryParsePin(args.Pin, out var pin))
            return;

        account.Pin = pin;
        account.PinSet = true;
        account.FailedPinAttempts = 0;
        BeginSession(atm);
        _audio.PlayPvs(atm.Comp.AcceptSound, atm.Owner);
        UpdateUiState(atm, Loc.GetString("is14-atm-status-pin-set"));
    }

    private void OnEnterPin(Entity<IS14AtmComponent> atm, ref IS14AtmEnterPinMessage args)
    {
        var account = GetInsertedAccount(atm, out _);
        if (account == null || !account.PinSet || atm.Comp.Authenticated || IsLocked(account))
            return;

        if (!TryParsePin(args.Pin, out var pin))
            return;

        if (pin == account.Pin)
        {
            account.FailedPinAttempts = 0;
            BeginSession(atm);
            _audio.PlayPvs(atm.Comp.AcceptSound, atm.Owner);
            UpdateUiState(atm);
            return;
        }

        account.FailedPinAttempts++;
        _audio.PlayPvs(atm.Comp.DenySound, atm.Owner);

        if (account.FailedPinAttempts >= atm.Comp.MaxPinAttempts)
        {
            account.FailedPinAttempts = 0;
            account.LockedUntil = _timing.CurTime + atm.Comp.LockDuration;
            EjectCardToHands(atm, args.Actor);
            UpdateUiState(atm, Loc.GetString("is14-atm-status-locked-now"));
            return;
        }

        UpdateUiState(atm, Loc.GetString("is14-atm-status-wrong-pin"));
    }

    private void OnChangePin(Entity<IS14AtmComponent> atm, ref IS14AtmChangePinMessage args)
    {
        var account = GetInsertedAccount(atm, out _);
        if (account == null || !atm.Comp.Authenticated)
            return;

        if (!TryParsePin(args.Pin, out var pin))
            return;

        account.Pin = pin;
        _audio.PlayPvs(atm.Comp.AcceptSound, atm.Owner);
        UpdateUiState(atm, Loc.GetString("is14-atm-status-pin-changed"));
    }

    // ── Operations ────────────────────────────────────────────────────────────

    private void OnWithdraw(Entity<IS14AtmComponent> atm, ref IS14AtmWithdrawMessage args)
    {
        var account = GetInsertedAccount(atm, out _);
        if (account == null || !atm.Comp.Authenticated || args.Amount <= 0)
            return;

        if (!_bankManager.TryChangeBalance(account.AccountNumber, -args.Amount, out _,
                Loc.GetString("economy-transaction-atm-withdraw"), atm.Owner))
        {
            _audio.PlayPvs(atm.Comp.DenySound, atm.Owner);
            UpdateUiState(atm, Loc.GetString("is14-atm-status-insufficient"));
            return;
        }

        var cash = _stack.Spawn(args.Amount, atm.Comp.CashStackType, Transform(atm.Owner).Coordinates);
        _hands.TryPickupAnyHand(args.Actor, cash);

        _audio.PlayPvs(atm.Comp.AcceptSound, atm.Owner);
        UpdateUiState(atm, Loc.GetString("is14-atm-status-withdraw-done"));
    }

    private void OnInteractUsing(Entity<IS14AtmComponent> atm, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Deposits: clicking the ATM with a cash stack.
        if (!TryComp<StackComponent>(args.Used, out var stack)
            || stack.StackTypeId != atm.Comp.CashStackType.Id
            || stack.Count <= 0)
        {
            return;
        }

        // The UI is power-gated; deposits bypass the UI, so check power here too.
        if (TryComp<ApcPowerReceiverComponent>(atm.Owner, out var power) && !power.Powered)
            return;

        args.Handled = true;

        var account = GetInsertedAccount(atm, out _);
        if (account == null || !atm.Comp.Authenticated)
        {
            _popup.PopupEntity(Loc.GetString("is14-atm-popup-need-auth"), atm.Owner, args.User);
            _audio.PlayPvs(atm.Comp.DenySound, atm.Owner);
            return;
        }

        var amount = stack.Count;
        if (!_bankManager.TryChangeBalance(account.AccountNumber, amount, out _,
                Loc.GetString("economy-transaction-atm-deposit"), atm.Owner))
        {
            return;
        }

        QueueDel(args.Used);
        _audio.PlayPvs(atm.Comp.AcceptSound, atm.Owner);
        _popup.PopupEntity(Loc.GetString("is14-atm-status-deposited", ("amount", amount)), atm.Owner, args.User);
        UpdateUiState(atm, Loc.GetString("is14-atm-status-deposited", ("amount", amount)));
    }

    private void OnTransfer(Entity<IS14AtmComponent> atm, ref IS14AtmTransferMessage args)
    {
        var account = GetInsertedAccount(atm, out _);
        if (account == null || !atm.Comp.Authenticated || args.Amount <= 0)
            return;

        if (args.TargetAccount == account.AccountNumber)
        {
            _audio.PlayPvs(atm.Comp.DenySound, atm.Owner);
            UpdateUiState(atm, Loc.GetString("is14-atm-status-own-account"));
            return;
        }

        if (_bankManager.GetAccount(args.TargetAccount) == null)
        {
            _audio.PlayPvs(atm.Comp.DenySound, atm.Owner);
            UpdateUiState(atm, Loc.GetString("is14-atm-status-bad-target"));
            return;
        }

        if (!_bankManager.TryChangeBalance(account.AccountNumber, -args.Amount, out _,
                Loc.GetString("economy-transaction-atm-transfer-out", ("target", args.TargetAccount)), atm.Owner))
        {
            _audio.PlayPvs(atm.Comp.DenySound, atm.Owner);
            UpdateUiState(atm, Loc.GetString("is14-atm-status-insufficient"));
            return;
        }

        _bankManager.TryChangeBalance(args.TargetAccount, args.Amount, out _,
            Loc.GetString("economy-transaction-atm-transfer-in", ("source", account.AccountNumber)), atm.Owner);

        _audio.PlayPvs(atm.Comp.AcceptSound, atm.Owner);
        UpdateUiState(atm, Loc.GetString("is14-atm-status-transfer-done"));
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private void UpdateUiState(Entity<IS14AtmComponent> atm, string status = "")
    {
        var screen = IS14AtmScreen.NoCard;
        var cardOwner = string.Empty;
        var accountNumber = 0;
        var balance = 0;
        var attemptsLeft = 0;
        var lockSecondsLeft = 0;

        var account = GetInsertedAccount(atm, out var card);
        if (card != null && TryComp<IdCardComponent>(card, out var idCard))
            cardOwner = idCard.FullName ?? string.Empty;

        if (account != null)
        {
            accountNumber = account.AccountNumber;

            if (IsLocked(account))
            {
                screen = IS14AtmScreen.Locked;
                lockSecondsLeft = (int)Math.Ceiling((account.LockedUntil!.Value - _timing.CurTime).TotalSeconds);
            }
            else if (!account.PinSet)
            {
                screen = IS14AtmScreen.SetPin;
            }
            else if (!atm.Comp.Authenticated)
            {
                screen = IS14AtmScreen.EnterPin;
                attemptsLeft = atm.Comp.MaxPinAttempts - account.FailedPinAttempts;
            }
            else
            {
                screen = IS14AtmScreen.Menu;
                balance = account.Balance;
            }
        }
        else if (card != null)
        {
            status = Loc.GetString("is14-atm-no-account");
        }

        _ui.SetUiState(atm.Owner, IS14AtmUiKey.Key,
            new IS14AtmUiState(screen, cardOwner, card != null ? GetNetEntity(card.Value) : null,
                accountNumber, balance, attemptsLeft, lockSecondsLeft, status));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks the session as authenticated and locks the card slot so the card
    /// can only leave via the eject button, not the alt-click verb.
    /// </summary>
    private void BeginSession(Entity<IS14AtmComponent> atm)
    {
        atm.Comp.Authenticated = true;
        _itemSlots.SetLock(atm.Owner, atm.Comp.CardSlotId, true);
    }

    private void EndSession(Entity<IS14AtmComponent> atm)
    {
        atm.Comp.Authenticated = false;
        _itemSlots.SetLock(atm.Owner, atm.Comp.CardSlotId, false);
    }

    private void EjectCardToHands(Entity<IS14AtmComponent> atm, EntityUid user)
    {
        if (_itemSlots.TryGetSlot(atm.Owner, atm.Comp.CardSlotId, out var slot))
            _itemSlots.TryEjectToHands(atm.Owner, slot, user);
    }

    private BankAccount? GetInsertedAccount(Entity<IS14AtmComponent> atm, out EntityUid? card)
    {
        card = _itemSlots.GetItemOrNull(atm.Owner, atm.Comp.CardSlotId);
        if (card == null || !TryComp<BankAccountHolderComponent>(card, out var holder))
            return null;

        return _bankManager.GetAccount(holder.AccountNumber);
    }

    private bool IsLocked(BankAccount account)
        => account.LockedUntil != null && account.LockedUntil.Value > _timing.CurTime;

    private static bool TryParsePin(string input, out int pin)
    {
        pin = 0;
        if (input.Length != 4)
            return false;

        foreach (var c in input)
        {
            if (c < '0' || c > '9')
                return false;
        }

        return int.TryParse(input, out pin);
    }
}

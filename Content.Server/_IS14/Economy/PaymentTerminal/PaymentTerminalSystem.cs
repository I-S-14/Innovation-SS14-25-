using Content.Server.Access.Systems;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.PaymentTerminal;
using Content.Shared.Access.Components;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.PaymentTerminal;

public sealed class PaymentTerminalSystem : EntitySystem
{
    [Dependency] private readonly BankingSystem _banking = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const int MaxAmount = 1_000_000;
    private const int MaxDescriptionLength = 64;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14PaymentTerminalComponent, InteractUsingEvent>(OnInteractUsing);

        Subs.BuiEvents<IS14PaymentTerminalComponent>(IS14PaymentTerminalUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<IS14PaymentTerminalSetChargeMessage>(OnSetCharge);
            subs.Event<IS14PaymentTerminalCancelMessage>(OnCancel);
            subs.Event<IS14PaymentTerminalPayMessage>(OnPay);
            subs.Event<IS14PaymentTerminalBindCardMessage>(OnBindCard);
        });
    }

    private void OnOpened(Entity<IS14PaymentTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        SendState(ent);
    }

    /// <summary>
    /// Tapping the terminal with a card (or a PDA holding one) pays the standing charge
    /// outright — no window, no button. With nothing to pay and no recipient yet, the tap
    /// binds the card instead, same as the button in the UI.
    /// </summary>
    private void OnInteractUsing(Entity<IS14PaymentTerminalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_idCard.TryGetIdCard(args.Used, out var idCard))
            return;

        args.Handled = true;

        if (ent.Comp.PendingAmount > 0)
        {
            TryPay(ent, idCard.Owner, args.User);
            return;
        }

        if (ent.Comp.RevenueAccount != null || ent.Comp.RecipientAccountNumber != null)
        {
            _popup.PopupEntity(Loc.GetString("is14-payterm-popup-nothing-to-pay"), ent.Owner, args.User);
            return;
        }

        BindCard(ent, idCard.Owner, args.User);
    }

    private void OnSetCharge(Entity<IS14PaymentTerminalComponent> ent, ref IS14PaymentTerminalSetChargeMessage args)
    {
        if (args.Amount <= 0 || args.Amount > MaxAmount)
        {
            SendState(ent, Loc.GetString("is14-payterm-status-bad-amount"));
            return;
        }

        var description = args.Description.Trim();
        if (description.Length > MaxDescriptionLength)
            description = description[..MaxDescriptionLength];

        ent.Comp.PendingAmount = args.Amount;
        ent.Comp.PendingDescription = description;
        SendState(ent);
    }

    private void OnCancel(Entity<IS14PaymentTerminalComponent> ent, ref IS14PaymentTerminalCancelMessage args)
    {
        ent.Comp.PendingAmount = 0;
        ent.Comp.PendingDescription = string.Empty;
        SendState(ent);
    }

    private void OnBindCard(Entity<IS14PaymentTerminalComponent> ent, ref IS14PaymentTerminalBindCardMessage args)
    {
        // Fund terminals have a fixed recipient.
        if (ent.Comp.RevenueAccount != null)
            return;

        if (!_idCard.TryFindIdCard(args.Actor, out var idCard))
        {
            Deny(ent, Loc.GetString("is14-payterm-status-no-card"));
            return;
        }

        BindCard(ent, idCard.Owner, args.Actor);
    }

    private void BindCard(Entity<IS14PaymentTerminalComponent> ent, EntityUid card, EntityUid user)
    {
        if (ent.Comp.RevenueAccount != null)
            return;

        if (!TryComp<BankAccountHolderComponent>(card, out var holder))
        {
            Deny(ent, Loc.GetString("is14-payterm-status-no-card"), user);
            return;
        }

        ent.Comp.RecipientAccountNumber = holder.AccountNumber;
        ent.Comp.RecipientName = CompOrNull<IdCardComponent>(card)?.FullName ?? string.Empty;
        ent.Comp.PendingAmount = 0;
        ent.Comp.PendingDescription = string.Empty;

        var status = Loc.GetString("is14-payterm-status-bound");
        _popup.PopupEntity(status, ent.Owner, user);
        SendState(ent, status);
    }

    private void OnPay(Entity<IS14PaymentTerminalComponent> ent, ref IS14PaymentTerminalPayMessage args)
    {
        TryPay(ent, args.Actor, args.Actor);
    }

    /// <summary>
    /// Charges the standing amount. <paramref name="payerSource"/> is whatever the account is
    /// read from: the customer themselves for the UI button, or the tapped card for a tap.
    /// </summary>
    private void TryPay(Entity<IS14PaymentTerminalComponent> ent, EntityUid payerSource, EntityUid user)
    {
        var amount = ent.Comp.PendingAmount;
        if (amount <= 0)
            return;

        // Resolve the recipient before touching the payer's money.
        EntityUid? station = null;
        if (ent.Comp.RevenueAccount != null)
        {
            if (_station.GetOwningStation(ent.Owner) is not { } owningStation
                || _stationBank.GetStationAccount(owningStation, ent.Comp.RevenueAccount) == null)
            {
                Deny(ent, Loc.GetString("is14-payterm-status-no-recipient"), user);
                return;
            }

            station = owningStation;
        }
        else if (ent.Comp.RecipientAccountNumber == null)
        {
            Deny(ent, Loc.GetString("is14-payterm-status-no-recipient"), user);
            return;
        }

        var recipientLabel = GetRecipientName(ent);
        var paymentDescription = ent.Comp.PendingDescription.Length > 0
            ? Loc.GetString("economy-transaction-terminal-payment-desc", ("recipient", recipientLabel), ("desc", ent.Comp.PendingDescription))
            : Loc.GetString("economy-transaction-terminal-payment", ("recipient", recipientLabel));

        // One payment = one log group: the charge, the revenue and the tax collapse into a single monitor row.
        var paymentGroup = Guid.NewGuid();

        if (!_banking.TryChangeBalance(payerSource, -amount, out _, paymentDescription, ent.Owner, paymentGroup))
        {
            Deny(ent, Loc.GetString("is14-payterm-status-denied"), user);
            return;
        }

        CreditRecipient(ent, station, amount, paymentGroup);
        PrintReceipt(ent, recipientLabel, amount, ent.Comp.PendingDescription);

        ent.Comp.PendingAmount = 0;
        ent.Comp.PendingDescription = string.Empty;
        _audio.PlayPvs(ent.Comp.PaySound, ent.Owner);

        var paidStatus = Loc.GetString("is14-payterm-status-paid", ("amount", amount));
        _popup.PopupEntity(paidStatus, ent.Owner, user);
        SendState(ent, paidStatus);
    }

    /// <summary>
    /// Sends the payment to the fund or personal account, splitting off the treasury tax.
    /// The terminal must be on a station for the tax to apply; a personal terminal
    /// off-station just delivers the full amount.
    /// </summary>
    private void CreditRecipient(Entity<IS14PaymentTerminalComponent> ent, EntityUid? station, int amount, Guid paymentGroup)
    {
        station ??= _station.GetOwningStation(ent.Owner);

        var tax = station == null ? 0 : (int)MathF.Round(amount * ent.Comp.TaxPercent);
        var revenue = amount - tax;

        if (ent.Comp.RevenueAccount is { } fund)
        {
            if (fund.Id == StationBankAccountSystem.Treasury)
            {
                tax = amount;
            }
            else if (_stationBank.TryChangeStationBalance(station!.Value, fund, revenue, out var fundBalance))
            {
                var account = _stationBank.GetStationAccount(station.Value, fund)!;
                RaiseLocalEvent(new EconomyTransactionEvent(account.AccountNumber, revenue, fundBalance,
                    Loc.GetString("economy-transaction-terminal-revenue"), ent.Owner, paymentGroup));
            }
        }
        else
        {
            _banking.TryChangeAccountBalance(ent.Comp.RecipientAccountNumber!.Value, revenue,
                Loc.GetString("economy-transaction-terminal-revenue"), ent.Owner, paymentGroup);
        }

        if (tax > 0 && station != null
            && _stationBank.TryChangeStationBalance(station.Value, StationBankAccountSystem.Treasury, tax, out var treasuryBalance))
        {
            var treasury = _stationBank.GetStationAccount(station.Value, StationBankAccountSystem.Treasury)!;
            RaiseLocalEvent(new EconomyTransactionEvent(treasury.AccountNumber, tax, treasuryBalance,
                Loc.GetString("economy-transaction-terminal-tax"), ent.Owner, paymentGroup));
        }
    }

    private void PrintReceipt(Entity<IS14PaymentTerminalComponent> ent, string recipient, int amount, string description)
    {
        if (ent.Comp.ReceiptPrototype is not { } proto)
            return;

        var receipt = Spawn(proto, Transform(ent.Owner).Coordinates);
        var note = description.Length > 0
            ? Loc.GetString("is14-payterm-receipt-note", ("desc", description))
            : string.Empty;

        _paper.SetContent(receipt,
            Loc.GetString("is14-payterm-receipt-content", ("recipient", recipient), ("amount", amount), ("note", note)));
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
    }

    private void Deny(Entity<IS14PaymentTerminalComponent> ent, string status, EntityUid? user = null)
    {
        _audio.PlayPvs(ent.Comp.DenySound, ent.Owner);

        // Card taps happen with no window open, so the reason has to reach the user as a popup.
        if (user != null)
            _popup.PopupEntity(status, ent.Owner, user.Value);

        SendState(ent, status);
    }

    private string GetRecipientName(Entity<IS14PaymentTerminalComponent> ent)
    {
        if (ent.Comp.RevenueAccount is { } fund)
        {
            return _prototypes.TryIndex(fund, out var proto)
                ? proto.DisplayName
                : fund.Id;
        }

        return ent.Comp.RecipientName;
    }

    private void SendState(Entity<IS14PaymentTerminalComponent> ent, string status = "")
    {
        var hasRecipient = ent.Comp.RevenueAccount != null || ent.Comp.RecipientAccountNumber != null;

        var recipientLabel = hasRecipient
            ? Loc.GetString("is14-payterm-recipient", ("name", GetRecipientName(ent)))
            : Loc.GetString("is14-payterm-no-recipient");

        _ui.SetUiState(ent.Owner, IS14PaymentTerminalUiKey.Key,
            new IS14PaymentTerminalUiState(
                recipientLabel,
                hasRecipient,
                ent.Comp.RevenueAccount == null,
                ent.Comp.PendingAmount,
                ent.Comp.PendingDescription,
                status));
    }
}

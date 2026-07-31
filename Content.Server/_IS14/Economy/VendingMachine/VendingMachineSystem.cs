using Content.Server.Access.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.VendingMachine;

public sealed class VendingMachineSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly BankingSystem _banking = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14VendingMachineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IS14VendingMachineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<IS14VendingMachineComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<IS14VendingMachineComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<IS14VendingMachineComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<IS14VendingMachineComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);

        Subs.BuiEvents<IS14VendingMachineComponent>(IS14VendingMachineUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<IS14VendingMachineBuyMessage>(OnBuy);
            subs.Event<IS14VendingMachineEjectCashMessage>(OnEjectCash);
        });
    }

    /// <summary>Reverts expired deny animations back to the normal screen.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<IS14VendingMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.DenyEnd == TimeSpan.Zero || now < comp.DenyEnd)
                continue;

            comp.DenyEnd = TimeSpan.Zero;
            UpdateVisualState((uid, comp));
        }
    }

    private void OnMapInit(Entity<IS14VendingMachineComponent> ent, ref MapInitEvent args)
    {
        UpdateVisualState(ent);
    }

    private void OnPowerChanged(Entity<IS14VendingMachineComponent> ent, ref PowerChangedEvent args)
    {
        UpdateVisualState(ent);
    }

    private void OnBreak(Entity<IS14VendingMachineComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.Broken = true;
        ent.Comp.DenyEnd = TimeSpan.Zero;
        // A smashed machine spills its till instead of eating the customer's money.
        DropCash(ent);
        _ui.CloseUi(ent.Owner, IS14VendingMachineUiKey.Key);
        UpdateVisualState(ent);
    }

    private void OnDamageChanged(Entity<IS14VendingMachineComponent> ent, ref DamageChangedEvent args)
    {
        // Repaired (damage went down below the breakage threshold via welding).
        if (!args.DamageIncreased && ent.Comp.Broken)
        {
            ent.Comp.Broken = false;
            UpdateVisualState(ent);
        }
    }

    private void OnUiOpenAttempt(Entity<IS14VendingMachineComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (ent.Comp.Broken)
            args.Cancel();
    }

    private void OnUiOpened(Entity<IS14VendingMachineComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(ent, args.Actor);
    }

    /// <summary>
    /// Walking away from the machine takes the change with you: the till is emptied back to
    /// whoever filled it once the last window closes, so nothing is left for the next customer.
    /// </summary>
    private void OnUiClosed(Entity<IS14VendingMachineComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.InsertedCash <= 0 || _ui.IsUiOpen(ent.Owner, IS14VendingMachineUiKey.Key))
            return;

        EjectCash(ent, ent.Comp.CashOwner ?? args.Actor);
    }

    /// <summary>Feeding the machine: clicking it with a cash stack tops up the till.</summary>
    private void OnInteractUsing(Entity<IS14VendingMachineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !ent.Comp.AcceptsCash
            || !TryComp<StackComponent>(args.Used, out var stack)
            || stack.StackTypeId != ent.Comp.CashStackType.Id
            || stack.Count <= 0)
        {
            return;
        }

        args.Handled = true;

        if (ent.Comp.Broken || !Transform(ent.Owner).Anchored || !_power.IsPowered(ent.Owner))
        {
            Deny(ent);
            return;
        }

        // The till belongs to the last person who put money in — see OnUiClosed.
        ent.Comp.InsertedCash += stack.Count;
        ent.Comp.CashOwner = args.User;
        QueueDel(args.Used);

        _audio.PlayPvs(ent.Comp.InsertCashSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("is14-vending-cash-inserted", ("amount", ent.Comp.InsertedCash)), ent.Owner, args.User);
        RefreshOpenUis(ent);
    }

    private void OnEjectCash(Entity<IS14VendingMachineComponent> ent, ref IS14VendingMachineEjectCashMessage args)
    {
        if (ent.Comp.InsertedCash <= 0)
            return;

        EjectCash(ent, args.Actor);
    }

    /// <summary>Empties the till into someone's hands, dropping it at the machine if they can't take it.</summary>
    private void EjectCash(Entity<IS14VendingMachineComponent> ent, EntityUid user)
    {
        var amount = ent.Comp.InsertedCash;
        if (amount <= 0)
            return;

        ent.Comp.InsertedCash = 0;
        ent.Comp.CashOwner = null;

        var cash = _stack.SpawnAtPosition(amount, ent.Comp.CashStackType, Transform(ent.Owner).Coordinates);

        // Whoever is standing there gets it in hand; if the owner already wandered off,
        // the money is left in the machine's tile rather than teleported across the station.
        if (!TerminatingOrDeleted(user) && _interaction.InRangeUnobstructed(user, ent.Owner))
            _hands.TryPickupAnyHand(user, cash);

        _audio.PlayPvs(ent.Comp.EjectCashSound, ent.Owner);
        RefreshOpenUis(ent);
    }

    /// <summary>Spills the till on the floor — used when the machine breaks with money inside.</summary>
    private void DropCash(Entity<IS14VendingMachineComponent> ent)
    {
        if (ent.Comp.InsertedCash <= 0)
            return;

        _stack.SpawnAtPosition(ent.Comp.InsertedCash, ent.Comp.CashStackType, Transform(ent.Owner).Coordinates);
        ent.Comp.InsertedCash = 0;
        ent.Comp.CashOwner = null;
    }

    private void RefreshOpenUis(Entity<IS14VendingMachineComponent> ent)
    {
        foreach (var actor in _ui.GetActors(ent.Owner, IS14VendingMachineUiKey.Key))
            UpdateUiState(ent, actor);
    }

    private void OnBuy(Entity<IS14VendingMachineComponent> ent, ref IS14VendingMachineBuyMessage args)
    {
        var buyer = args.Actor;

        // The UI can stay open while the machine breaks, is unwrenched or loses power — recheck here.
        if (ent.Comp.Broken || !Transform(ent.Owner).Anchored || !_power.IsPowered(ent.Owner))
        {
            Deny(ent);
            return;
        }

        List<VendingMachineEntry>? inventory;
        if (args.TabIndex < 0)
        {
            inventory = ent.Comp.ContrabandUnlocked ? ent.Comp.ContrabandInventory : null;
        }
        else if (args.TabIndex < ent.Comp.Tabs.Count)
        {
            var tab = ent.Comp.Tabs[args.TabIndex];
            inventory = HasTabAccess(buyer, tab) ? tab.Inventory : null;
        }
        else
        {
            inventory = null;
        }

        if (inventory == null || args.ItemIndex < 0 || args.ItemIndex >= inventory.Count)
        {
            Deny(ent);
            return;
        }

        var entry = inventory[args.ItemIndex];

        if (entry.Stock <= 0)
        {
            Deny(ent);
            return;
        }

        var itemName = entry.ItemId.ToString();
        if (_prototypes.TryIndex<EntityPrototype>(entry.ItemId, out var entProto))
            itemName = entProto.Name;
        var purchaseDescription = Loc.GetString("economy-transaction-vending-purchase", ("item", itemName), ("machine", GetMachineName(ent)));

        // One purchase = one log group: payment, fund revenue and tax collapse into a single monitor row.
        var purchaseGroup = Guid.NewGuid();

        // Money in the till always wins over the card, and it has no account behind it:
        // a cash sale leaves no payer row in the monitor, only revenue and tax.
        var cash = HasCash(ent);
        var paid = cash
            ? TryPayCash(ent, entry.Price)
            : _banking.TryChangeBalance(buyer, -entry.Price, out _, purchaseDescription, ent.Owner, purchaseGroup);

        if (!paid)
        {
            Deny(ent);
            return;
        }

        entry.Stock--;
        CreditRevenue(ent, entry.Price, itemName, purchaseGroup, cash);

        var item = Spawn(entry.ItemId, Transform(ent.Owner).Coordinates);
        if (!_hands.TryPickupAnyHand(buyer, item))
            _transform.SetCoordinates(item, Transform(buyer).Coordinates);

        _audio.PlayPvs(ent.Comp.BuySound, ent.Owner);
        UpdateUiState(ent, buyer);
    }

    /// <summary>Whether the machine is running on cash right now instead of on cards.</summary>
    private static bool HasCash(Entity<IS14VendingMachineComponent> ent)
        => ent.Comp.AcceptsCash && ent.Comp.InsertedCash > 0;

    /// <summary>Takes the price out of the till. The remainder stays in the machine as change.</summary>
    private bool TryPayCash(Entity<IS14VendingMachineComponent> ent, int price)
    {
        if (ent.Comp.InsertedCash < price)
            return false;

        ent.Comp.InsertedCash -= price;
        return true;
    }

    /// <summary>UI display name: the component override or the entity name.</summary>
    private string GetMachineName(Entity<IS14VendingMachineComponent> ent)
        => string.IsNullOrWhiteSpace(ent.Comp.MachineName) ? Name(ent.Owner) : ent.Comp.MachineName;

    /// <summary>Plays the deny sound and flashes the deny screen state, like vanilla vending machines.</summary>
    private void Deny(Entity<IS14VendingMachineComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.DenySound, ent.Owner);

        if (ent.Comp.Broken || !_power.IsPowered(ent.Owner))
            return;

        ent.Comp.DenyEnd = _timing.CurTime + ent.Comp.DenyDelay;
        UpdateVisualState(ent);
    }

    private void UpdateVisualState(Entity<IS14VendingMachineComponent> ent)
    {
        var state = IS14VendingMachineVisualState.Normal;

        if (ent.Comp.Broken)
            state = IS14VendingMachineVisualState.Broken;
        else if (!_power.IsPowered(ent.Owner))
            state = IS14VendingMachineVisualState.Off;
        else if (ent.Comp.DenyEnd != TimeSpan.Zero && _timing.CurTime < ent.Comp.DenyEnd)
            state = IS14VendingMachineVisualState.Deny;

        _appearance.SetData(ent.Owner, IS14VendingMachineVisuals.VisualState, state);
    }

    /// <summary>
    /// Splits a sale between the machine's revenue fund and the treasury tax.
    /// If the machine has no owning station or accounts, the money leaves the economy (sink).
    /// </summary>
    private void CreditRevenue(Entity<IS14VendingMachineComponent> ent, int price, string itemName, Guid purchaseGroup, bool cash)
    {
        if (price <= 0)
            return;

        if (_station.GetOwningStation(ent.Owner) is not { } station)
            return;

        var tax = (int)MathF.Round(price * ent.Comp.TaxPercent);
        var revenue = price - tax;
        var wholeSaleToTreasury = ent.Comp.RevenueAccount is not { } configured || configured.Id == StationBankAccountSystem.Treasury;

        // A cash sale has no payer row, so the revenue line is the only record of the purchase —
        // it says what was sold and that it was paid in cash.
        var revenueDescription = Loc.GetString(
            cash ? "economy-transaction-vending-revenue-cash" : "economy-transaction-vending-revenue",
            ("item", itemName), ("machine", GetMachineName(ent)));

        if (!wholeSaleToTreasury)
        {
            var fund = ent.Comp.RevenueAccount!.Value;
            if (_stationBank.TryChangeStationBalance(station, fund, revenue, out var fundBalance))
            {
                var account = _stationBank.GetStationAccount(station, fund)!;
                RaiseLocalEvent(new EconomyTransactionEvent(account.AccountNumber, revenue, fundBalance,
                    revenueDescription, ent.Owner, purchaseGroup));
            }
        }
        else
        {
            // No dedicated fund — the whole sale goes to the treasury.
            tax = price;
        }

        if (tax > 0 && _stationBank.TryChangeStationBalance(station, StationBankAccountSystem.Treasury, tax, out var treasuryBalance))
        {
            var treasury = _stationBank.GetStationAccount(station, StationBankAccountSystem.Treasury)!;
            RaiseLocalEvent(new EconomyTransactionEvent(treasury.AccountNumber, tax, treasuryBalance,
                wholeSaleToTreasury
                    ? revenueDescription
                    : Loc.GetString("economy-transaction-vending-tax", ("machine", GetMachineName(ent))),
                ent.Owner, purchaseGroup));
        }
    }

    public void SetContraband(EntityUid uid, bool value, IS14VendingMachineComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.ContrabandUnlocked = value;

        foreach (var actor in _ui.GetActors(uid, IS14VendingMachineUiKey.Key))
            UpdateUiState((uid, comp), actor);
    }

    private void UpdateUiState(Entity<IS14VendingMachineComponent> ent, EntityUid actor)
    {
        _banking.TryGetEntityBalance(actor, out var balance);
        var (playerName, playerJob, playerIdCard) = GetPlayerInfo(actor);

        var tabs = new List<IS14VendingTabUiState>(ent.Comp.Tabs.Count);
        foreach (var tab in ent.Comp.Tabs)
            tabs.Add(new IS14VendingTabUiState(tab.Name, BuildUiEntries(tab.Inventory), HasTabAccess(actor, tab)));

        var adMessage = string.Empty;
        if (ent.Comp.AdMessages.Count > 0)
        {
            var key = _random.Pick(ent.Comp.AdMessages);
            adMessage = _loc.GetString(key);
        }

        _ui.SetUiState(ent.Owner, IS14VendingMachineUiKey.Key,
            new IS14VendingMachineUiState(
                GetMachineName(ent),
                balance,
                ent.Comp.AcceptsCash,
                ent.Comp.InsertedCash,
                ent.Comp.ContrabandUnlocked,
                tabs,
                BuildUiEntries(ent.Comp.ContrabandInventory),
                playerName,
                playerJob,
                playerIdCard,
                adMessage));
    }

    /// <summary>
    /// Checks whether the buyer's access tags (ID card, held items) satisfy the tab's
    /// access requirements. Tabs without requirements are open to everyone.
    /// </summary>
    private bool HasTabAccess(EntityUid buyer, VendingMachineTab tab)
    {
        if (tab.Access.Count == 0)
            return true;

        var tags = _accessReader.FindAccessTags(buyer);
        foreach (var access in tab.Access)
        {
            if (tags.Contains(access))
                return true;
        }

        return false;
    }

    private (string name, string job, NetEntity? idCard) GetPlayerInfo(EntityUid actor)
    {
        if (!_idCard.TryFindIdCard(actor, out var idCard) || !TryComp<IdCardComponent>(idCard, out var card))
            return (_loc.GetString("is14-vending-unknown"), string.Empty, null);

        return (card.FullName ?? _loc.GetString("is14-vending-unknown"),
            card.LocalizedJobTitle ?? string.Empty,
            GetNetEntity(idCard));
    }

    private List<IS14VendingMachineUiEntry> BuildUiEntries(List<VendingMachineEntry> source)
    {
        var result = new List<IS14VendingMachineUiEntry>(source.Count);
        foreach (var entry in source)
        {
            var name = entry.ItemId.ToString();
            if (_prototypes.TryIndex<EntityPrototype>(entry.ItemId, out var proto))
                name = proto.Name;

            result.Add(new IS14VendingMachineUiEntry(entry.ItemId, name, entry.Price, entry.Stock));
        }
        return result;
    }
}

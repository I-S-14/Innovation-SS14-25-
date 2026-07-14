using Content.Server.Access.Systems;
using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._IS14.Economy.VendingMachine;

public sealed class VendingMachineSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly BankingSystem _banking = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14VendingMachineComponent, ActivateInWorldEvent>(OnActivate);

        Subs.BuiEvents<IS14VendingMachineComponent>(IS14VendingMachineUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<IS14VendingMachineBuyMessage>(OnBuy);
        });
    }

    private void OnActivate(Entity<IS14VendingMachineComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.TryOpenUi(ent.Owner, IS14VendingMachineUiKey.Key, args.User);
    }

    private void OnUiOpened(Entity<IS14VendingMachineComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(ent, args.Actor);
    }

    private void OnBuy(Entity<IS14VendingMachineComponent> ent, ref IS14VendingMachineBuyMessage args)
    {
        var buyer = args.Actor;

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
            _audio.PlayPvs(ent.Comp.DenySound, ent.Owner);
            return;
        }

        var entry = inventory[args.ItemIndex];

        var itemName = entry.ItemId.ToString();
        if (_prototypes.TryIndex<EntityPrototype>(entry.ItemId, out var entProto))
            itemName = entProto.Name;
        var purchaseDescription = Loc.GetString("economy-transaction-vending-purchase", ("item", itemName), ("machine", ent.Comp.MachineName));

        if (entry.Stock <= 0 || !_banking.TryChangeBalance(buyer, -entry.Price, out _, purchaseDescription, ent.Owner))
        {
            _audio.PlayPvs(ent.Comp.DenySound, ent.Owner);
            return;
        }

        entry.Stock--;

        var item = Spawn(entry.ItemId, Transform(ent.Owner).Coordinates);
        if (!_hands.TryPickupAnyHand(buyer, item))
            _transform.SetCoordinates(item, Transform(buyer).Coordinates);

        _audio.PlayPvs(ent.Comp.BuySound, ent.Owner);
        UpdateUiState(ent, buyer);
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
                ent.Comp.MachineName,
                balance,
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

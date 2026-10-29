// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Pirates;
using Content.Server.Access.Systems;
using Content.Goobstation.Common.AlmanacBlade; // Goobstation
using Content.Server.Cargo.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Containers; // CorvaxGoob-CargoFeatures
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.EntityTable; // CorvaxGoob-CargoFeatures
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;
using Content.Shared.Prototypes;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems; // CorvaxGoob-CargoFeatures
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // CorvaxGoob-CargoFeatures-Start
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    // CorvaxGoob-CargoFeatures-End

    [Dependency] private readonly StorageSystem _storage = default!;
    private ISawmill _sawmill = default!;

    private void InitializeConsole()
    {
        var station = _station.GetOwningStation(ent.Owner);
        UpdateOrderState(ent.Owner, station);
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<CargoOrderConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (_cashQuery.HasComp(args.Used))
        {
            OnInteractUsingCash(ent, ref args);
        }
        else if (
            _slipQuery.TryComp(args.Used, out var slip)
            && ent.Comp.Mode == CargoOrderConsoleMode.DirectOrder
        )
        {
            OnInteractUsingSlip(ent, ref args, slip);
        }
    }

    [SubscribeLocalEvent]
    private void OnEmagged(Entity<CargoOrderConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnRemoveOrderMessage(Entity<CargoOrderConsoleComponent> ent, ref CargoConsoleRemoveOrderMessage args)
    {
        var station = _station.GetOwningStation(ent.Owner);

        if (ent.Comp.Mode == CargoOrderConsoleMode.PrintSlip)
            return;

        if (!OnSameMap(ent.Owner, station) || !TryGetOrderDatabase(station, out var orderDatabase))
            return;

        if (!_bankQuery.TryComp(station, out var bank))
            return;

        var orderId = GenerateOrderId(orderDatabase);

        // CorvaxGoob-CargoFeatures : Добавлено поле доставки, примечания, защищённости и наценка за безопасную доставку 
        var data = new CargoOrderData(orderId, product.Product, product.Name, product.Cost + (slip.SecuredDelivery ? ent.Comp.SecureOrderCost : 0), slip.OrderQuantity, slip.Requester, slip.DeliveryDestination, slip.Note, slip.Account, product.Cooldown, slip.SecuredDelivery);

        if (!TryAddOrder(stationUid.Value, ent.Comp.Account, data, orderDatabase))
        {
            PlayDenySound(ent, ent.Comp);
            return;
        }

        // Log order addition
        _audio.PlayPvs(ent.Comp.ScanSound, ent);
        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            // CorvaxGoob-CargoFeatures : Рефактор старых названий
            $"{ToPrettyString(args.User):user} inserted order slip [orderId:{data.OrderId}, quantity:{data.OrderQuantity}, product:{data.ProductId}, requester:{data.Requester}, deliveryDestination: {data.DeliveryDestination}, note:{data.Note}] ");
        QueueDel(args.Used);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnOrderUIOpened(Entity<CargoOrderConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        var station = _station.GetOwningStation(ent.Owner);
        UpdateOrderState(ent.Owner, station);
    }

    [SubscribeLocalEvent]
    private void OnAddOrderMessage(Entity<CargoOrderConsoleComponent> ent, ref CargoConsoleAddOrderMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (args.Amount <= 0)
            return;

        var stationUid = _station.GetOwningStation(ent.Owner);
        if (!OnSameMap(ent.Owner, stationUid) || !TryGetOrderDatabase(stationUid, out var orderDatabase))
            return;

        if (!_bankQuery.TryComp(stationUid, out var bank))
            return;

        if (!ProtoMan.TryIndex<CargoProductPrototype>(args.CargoProductId, out var product))
        {
            Log.Error($"Tried to add invalid cargo product {args.CargoProductId} as order!");
            return;
        }

        if (!GetAvailableProducts(ent).Contains(args.CargoProductId))
            return;

        if (ent.Comp.Mode == CargoOrderConsoleMode.PrintSlip)
        {
            OnAddOrderMessageSlipPrinter(ent, args, product);
            return;
        }

        var targetAccount =
            ent.Comp.Mode == CargoOrderConsoleMode.SendToPrimary ? bank.PrimaryAccount : ent.Comp.Account;
        var data = GetOrderData(args, product, GenerateOrderId(orderDatabase), ent.Comp.Account);
        if (!TryAddOrder(stationUid.Value, targetAccount, data, orderDatabase))
        {
            PlayDenySound(ent);
            return;
        }

        // Log order addition
        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(player):user} added order [orderId:{data.OrderId}, quantity:{data.OrderQuantity}, product:{data.Product}, requester:{data.Requester}, reason:{data.Reason}]"
        );
    }

    [SubscribeLocalEvent]
    private void OnApproveOrderMessage(Entity<CargoOrderConsoleComponent> ent, ref CargoConsoleApproveOrderMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (ent.Comp.Mode != CargoOrderConsoleMode.DirectOrder)
            return;

        if (!_accessReaderSystem.IsAllowed(player, ent.Owner))
        {
            _popup.PopupCursor(Loc.GetString("cargo-console-order-not-allowed"), args.Actor);
            PlayDenySound(ent);
            return;
        }

        // goob edit - resource siphon blocking access
        var eqe = EntityQueryEnumerator<ResourceSiphonComponent>();
        while (eqe.MoveNext(out var sip))
        {
            // it's over. the crew won't know what hit em.
            if (sip.Active)
            {
                ConsolePopup(args.Actor, Loc.GetString("console-block-something"));
                PlayDenySound(uid, component);
                return;
            }
        }
        // goob edit end

        var station = _station.GetOwningStation(uid);

        // No station to deduct from.
        if (
            !OnSameMap(ent.Owner, station)
            || !_bankQuery.TryComp(station, out var bank)
            || !_stationQuery.TryComp(station, out var stationData)
            || !TryGetOrderDatabase(station, out var orderDatabase)
        )
        {
            _popup.PopupCursor(Loc.GetString("cargo-console-station-not-found"), args.Actor);
            PlayDenySound(ent);
            return;
        }

        // Find our order again. It might have been dispatched or approved already
        var orderId = args.OrderId;
        var order = orderDatabase.Orders[ent.Comp.Account].Find(order => orderId == order.OrderId && !order.Approved);
        if (order == null || !ProtoMan.Resolve(order.Account, out var account))
        {
            return;
        }

        // Invalid order
        if (!ProtoMan.Resolve(order.Product, out var product))
        {
            _popup.PopupCursor(Loc.GetString("cargo-console-invalid-product"), args.Actor);
            PlayDenySound(ent);
            return;
        }

        var amount = GetOutstandingOrderCount((station.Value, orderDatabase), order.Account);
        var capacity = orderDatabase.Capacity;

        // Too many orders, avoid them getting spammed in the UI.
        if (amount >= capacity)
        {
            _popup.PopupCursor(Loc.GetString("cargo-console-too-many"), args.Actor);
            PlayDenySound(ent);
            return;
        }

        // Cap orders so someone can't spam thousands.
        var cappedAmount = Math.Min(capacity - amount, order.OrderQuantity);
        if (cappedAmount != order.OrderQuantity)
        {
            order.OrderQuantity = cappedAmount;
            _popup.PopupCursor(Loc.GetString("cargo-console-snip-snip"), args.Actor);
            PlayDenySound(ent);
        }

        var cost = product.Cost * order.OrderQuantity;
        var accountBalance = GetBalanceFromAccount((station.Value, bank), order.Account);

        // Not enough balance
        if (cost > accountBalance)
        {
            _popup.PopupCursor(Loc.GetString("cargo-console-insufficient-funds", ("cost", cost)), args.Actor);
            PlayDenySound(ent);
            return;
        }

        // GoobStation - cooldown on Cargo Orders (specifically gamba)
        if (order.Cooldown > 0)
        {
            if (orderDatabase.ProductCooldownTime.TryGetValue(order.ProductId, out var cooldownTime) && cooldownTime > _timing.CurTime)
            {
                var timeLeft = (cooldownTime - _timing.CurTime);
                (int count, string units) timeInfo = (timeLeft.Minutes > 0) ? (timeLeft.Minutes, "minutes") : (timeLeft.Seconds, "seconds");
                ConsolePopup(args.Actor, Loc.GetString("cargo-console-cooldown-active", ("product", order.ProductName), ("timeCount", timeInfo.count), ("timeUnits", timeInfo.units)));
                PlayDenySound(uid, component);
                return;
            }
            if (order.OrderQuantity > 1)
            {
                ConsolePopup(args.Actor, Loc.GetString("cargo-console-cooldown-count", ("product", order.ProductName)));
                PlayDenySound(uid, component);
                return;
            }
        }

        var ev = new FulfillCargoOrderEvent((station.Value, stationData), order, (uid, component));
        RaiseLocalEvent(ref ev);
        ev.FulfillmentEntity ??= station.Value;

        if (!ev.Handled)
        {
            ev.FulfillmentEntity = TryFulfillOrder((station.Value, stationData), order.Account, order, orderDatabase);

            if (ev.FulfillmentEntity == null)
            {
                _popup.PopupCursor(Loc.GetString("cargo-console-unfulfilled"), args.Actor);
                PlayDenySound(ent);
                order.Approver = null;
                order.ApprovingConsole = null;
                order.Approved = false;
                return;
            }
        }

        // GoobStation - cooldown on Cargo Orders (specifically gamba)
        if (order.Cooldown > 0)
        {
            orderDatabase.ProductCooldownTime[order.ProductId] = _timing.CurTime + TimeSpan.FromSeconds(order.Cooldown);
        }

        order.Approved = true;
        _audio.PlayPvs(ApproveSound, uid);

        if (!emagged)
        {
            var message = Loc.GetString(
                "cargo-console-unlock-approved-order-broadcast",
                ("productName", Loc.GetString(product.Name)),
                ("orderAmount", order.OrderQuantity),
                ("approver", order.Approver ?? string.Empty),
                ("cost", cost)
            );
            _radio.SendRadioMessage(ent.Owner, message, account.RadioChannel, ent.Owner, escapeMarkup: false);
            if (CargoOrderConsoleComponent.BaseAnnouncementChannel != account.RadioChannel)
            {
                _radio.SendRadioMessage(
                    ent.Owner,
                    message,
                    CargoOrderConsoleComponent.BaseAnnouncementChannel,
                    ent.Owner,
                    escapeMarkup: false
                );
            }
        }

        _popup.PopupCursor(
            Loc.GetString(
                "cargo-console-trade-station",
                ("destination", MetaData(ev.FulfillmentEntity.Value).EntityName)
            ),
            args.Actor
        );

        // Log order approval
        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            // CorvaxGoob-CargoFeatures : Рефактор старых названий
            $"{ToPrettyString(player):user} approved order [orderId:{order.OrderId}, quantity:{order.OrderQuantity}, product:{order.ProductId}, requester:{order.Requester}, deliveryDestination: {order.DeliveryDestination}, note:{order.Note}] on account {order.Account} with balance at {accountBalance}");

        orderDatabase.Orders[ent.Comp.Account].Remove(order);
        UpdateBankAccount((station.Value, bank), -cost, order.Account);
        UpdateOrders(station.Value);
    }

    /// <summary>
    /// Tries to fulfill the next outstanding order.
    /// </summary>
    [PublicAPI]
    private bool FulfillNextOrder(
        StationCargoOrderDatabaseComponent orderDb,
        ProtoId<CargoAccountPrototype> account,
        EntityCoordinates spawn,
        string? paperProto
    )
    {
        if (!PopFrontOrder(orderDb, account, out var order))
            return false;

            var freePads = GetFreeCargoPallets(trade, tradePads);
            if (freePads.Count >= order.OrderQuantity) //check if the station has enough free pallets
            {
                // CorvaxGoob-CargoFeatures-Start
                EntityUid? previousCrate = null;
                List<string>? excessItems = null;
                // CorvaxGoob-CargoFeatures-End

                foreach (var pad in freePads)
                {
                    var coordinates = new EntityCoordinates(trade, pad.Transform.LocalPosition);

                    // CorvaxGoob-CargoFeatures-Start
                    if (previousCrate is not null
                        && TryComp<EntityStorageComponent>(previousCrate, out var entityStorage)
                        && entityStorage.Contents.Count >= entityStorage.Capacity)
                    {
                        previousCrate = null;
                    }
                    // CorvaxGoob-CargoFeatures-End

                    // CorvaxGoob-CargoFeatures
                    if (FulfillOrder(order, account, coordinates, orderDatabase.PrinterOutput, out var nextCrate, out excessItems, previousCrate, excessItems))
                    {
                        previousCrate = nextCrate; // CorvaxGoob-CargoFeatures
                        tradeDestination = trade;
                        order.NumDispatched++;
                        if (order.OrderQuantity <= order.NumDispatched) //Spawn a crate on free pellets until the order is fulfilled.
                            break;
                    }
                }
            }

    public void RemoveOrder(
        EntityUid dbUid,
        ProtoId<CargoAccountPrototype> account,
        int index,
        StationCargoOrderDatabaseComponent orderDb
    )
    {
        var sequenceIdx = orderDb.Orders[account].FindIndex(order => order.OrderId == index);
        if (sequenceIdx != -1)
        {
            orderDb.Orders[account].RemoveAt(sequenceIdx);
        }
        UpdateOrders(dbUid);
    }

    public void ClearOrders(StationCargoOrderDatabaseComponent component)
    {
        if (component.Orders.Count == 0)
            return;

        component.Orders.Clear();
    }


    public int GetOutstandingOrderCount(
        Entity<StationCargoOrderDatabaseComponent> station,
        ProtoId<CargoAccountPrototype> account
    )
    {
        var amount = 0;

        if (!_bankQuery.TryComp(station, out var bank))
            return amount;

        foreach (var order in station.Comp.Orders[account])
        {
            if (!order.Approved)
                continue;

            amount += order.OrderQuantity - order.NumDispatched;
        }

        if (account == bank.PrimaryAccount)
            return amount;

        foreach (var order in station.Comp.Orders[bank.PrimaryAccount])
        {
            if (order.Account != account || !order.Approved)
                continue;

            amount += order.OrderQuantity - order.NumDispatched;
        }

        return amount;
    }

    private void OnAddOrderMessageSlipPrinter(EntityUid uid, CargoOrderConsoleComponent component, CargoConsoleAddOrderMessage args, CargoProductPrototype product, string requester) // CorvaxGoob-CargoFeatures : Добавлено арг requester
    {
        if (station is null ||
            _station.GetLargestGrid(station.Value) is not { } stationGrid)
            return false;

        return Transform(stationGrid).MapID == Transform(uid).MapID;
    }

    public List<ProtoId<CargoProductPrototype>> GetAvailableProducts(Entity<CargoOrderConsoleComponent> ent)
    {
        if (
            _station.GetOwningStation(ent) is not { } station
            || !_orderQuery.TryComp(station, out var db)
        )
        {
            return [];
        }

        var products = new List<ProtoId<CargoProductPrototype>>();

        msg.AddMarkupPermissive(Loc.GetString("cargo-acquisition-slip-body",
            ("product", product.Name),
            ("description", product.Description),
            ("unit", product.Cost),
            ("amount", args.Amount),
            ("cost", product.Cost * args.Amount + (args.SecuredDelivery ? component.SecureOrderCost : 0)), // CorvaxGoob-CargoFeatures-Start : доп.цена
            ("orderer", requester),
            ("destination", args.DeliveryDestination ?? Loc.GetString("cargo-console-paper-delivery-destination-default")),
            ("note", args.Note ?? Loc.GetString("cargo-console-paper-note-default")))); // CorvaxGoob-CargoFeatures-End
        _paperSystem.SetContent((label, paper), msg.ToMarkup());

        var slip = EnsureComp<CargoSlipComponent>(label);
        slip.Product = product.ID;

        // CorvaxGoob-CargoFeatures-Start
        slip.Requester = requester;
        slip.DeliveryDestination = args.DeliveryDestination;
        slip.Note = args.Note;
        slip.SecuredDelivery = args.SecuredDelivery;
        // CorvaxGoob-CargoFeatures-End

        slip.OrderQuantity = args.Amount;
        slip.Account = component.Account;
    }

    public bool AddAndApproveOrder(
        EntityUid dbUid,
        CargoProductPrototype product,
        int qty,
        string sender,
        string description,
        string dest,
        StationCargoOrderDatabaseComponent component,
        ProtoId<CargoAccountPrototype> account,
        Entity<StationDataComponent> stationData
    )
    {
        // Make an order
        var id = GenerateOrderId(component);
        var order = new CargoOrderData(id, product, qty, sender, description, account);

        // Approve it now
        order.SetApproverData(dest, sender);
        order.Approved = true;

        // Log order addition
        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            $"AddAndApproveOrder {description} added order [orderId:{order.OrderId}, quantity:{order.OrderQuantity}, product:{order.Product}, requester:{order.Requester}, reason:{order.Reason}]"
        );

        // Add it to the list
        return TryAddOrder(dbUid, account, order, component)
            && TryFulfillOrder(stationData, account, order, component).HasValue;
    }

    private void OnInteractUsingSlip(
        Entity<CargoOrderConsoleComponent> ent,
        ref InteractUsingEvent args,
        CargoSlipComponent slip
    )
    {
        if (slip.OrderQuantity <= 0)
            return;

        var stationUid = _station.GetOwningStation(ent);

        if (!OnSameMap(ent.Owner, stationUid) || !TryGetOrderDatabase(stationUid, out var orderDatabase))
            return;

        if (args.SecuredDelivery) // CorvaxGoob-CargoFeatures : Валидация поля запроса
            args.SecuredDelivery = CanBeSecuredDelivery((uid, component), _protoMan.Index<CargoProductPrototype>(args.CargoProductId));


        // CorvaxGoob-CargoFeatures-Start
        string requester = string.Empty;
        if (component.EditableRequesterName && args.Requester is not null)
            requester = args.Requester;
        else
            requester = GenerateRequesterName((uid, component), args.Actor);
        // CorvaxGoob-CargoFeatures-End

        if (component.Mode == CargoOrderConsoleMode.PrintSlip)
        {
            OnAddOrderMessageSlipPrinter(uid, component, args, product, requester); // CorvaxGoob-CargoFeatures : арг. requester
            return;
        }

        if (!ent.Comp.AllowedGroups.Contains(product.Group))
            return;

        // CorvaxGoob-CargoFeatures : Добавлены новые аргументы
        var data = GetOrderData(args, product, GenerateOrderId(orderDatabase), component.Account, requester, args.SecuredDelivery ? component.SecureOrderCost : default);

        if (!TryAddOrder(stationUid.Value, ent.Comp.Account, data, orderDatabase))
        {
            PlayDenySound(ent);
            return;
        }

        // Log order addition
        _audio.PlayPvs(ent.Comp.ScanSound, ent);
        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            // CorvaxGoob-CargoFeatures : Рефактор старых названий
            $"{ToPrettyString(player):user} added order [orderId:{data.OrderId}, quantity:{data.OrderQuantity}, product:{data.ProductId}, requester:{data.Requester}, deliveryDestination: {data.DeliveryDestination}, note:{data.Note}]");

    }

    private void OnOrderUIOpened(EntityUid uid, CargoOrderConsoleComponent component, BoundUIOpenedEvent args)
    {
        var station = _station.GetOwningStation(uid);
        UpdateOrderState(uid, station);
    }

    private void OnInteractUsingCash(Entity<CargoOrderConsoleComponent> ent, ref InteractUsingEvent args)
    {
        var price = _pricing.GetPrice(args.Used);
        if (price == 0)
            return;

        var stationUid = _station.GetOwningStation(args.Used);

        if (!_bankQuery.TryComp(stationUid, out var bank))
            return;

        _audio.PlayPvs(ApproveSound, ent.Owner);
        UpdateBankAccount((stationUid.Value, bank), (int)price, ent.Comp.Account);
        QueueDel(args.Used);
        args.Handled = true;
    }

    private void OnAddOrderMessageSlipPrinter(
        Entity<CargoOrderConsoleComponent> ent,
        CargoConsoleAddOrderMessage args,
        CargoProductPrototype product
    )
    {
        if (!ProtoMan.Resolve(ent.Comp.Account, out var account))
            return;

        if (Timing.CurTime < ent.Comp.NextPrintTime)
            return;

        var label = Spawn(account.AcquisitionSlip, Transform(ent.Owner).Coordinates);
        ent.Comp.NextPrintTime = Timing.CurTime + ent.Comp.PrintDelay;
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);

        var paper = EnsureComp<PaperComponent>(label);
        var msg = new FormattedMessage();

        msg.AddMarkupPermissive(
            Loc.GetString(
                "cargo-acquisition-slip-body",
                ("product", product.Name),
                ("description", product.Description),
                ("unit", product.Cost),
                ("amount", args.Amount),
                ("cost", product.Cost * args.Amount),
                ("orderer", args.Requester),
                ("reason", args.Reason)
            )
        );
        _paperSystem.SetContent((label, paper), msg.ToMarkup());

    // CorvaxGoob-CargoFeatures-Start : Добавлены новые аргументы и параметры к данным
    private static CargoOrderData GetOrderData(CargoConsoleAddOrderMessage args, CargoProductPrototype cargoProduct, int id, ProtoId<CargoAccountPrototype> account, string requester, int extraPrice = 0)
    {
        // GoobStation - cooldown on Cargo Orders (specifically gamba)
        return new CargoOrderData(id, cargoProduct.Product, cargoProduct.Name, cargoProduct.Cost + extraPrice, args.Amount, requester, args.DeliveryDestination, args.Note, account, cargoProduct.Cooldown, args.SecuredDelivery);
    }
    // CorvaxGoob-CargoFeatures-End

    private EntityUid? TryFulfillOrder(
        Entity<StationDataComponent> stationData,
        ProtoId<CargoAccountPrototype> account,
        CargoOrderData order,
        StationCargoOrderDatabaseComponent orderDatabase
    )
    {
        // No slots at the trade station
        _listEnts.Clear();
        GetTradeStations(stationData, ref _listEnts);
        EntityUid? tradeDestination = null;

        // Try to fulfill from any station where possible, if the pad is not occupied.
        foreach (var trade in _listEnts)
        {
            var freePads = GetFreeCargoPallets(trade, BuySellType.Buy).ToList();
            if (freePads.Count < order.OrderQuantity) //check if the station has enough free pallets
                continue;

            _random.Shuffle(freePads);
            foreach (var pad in freePads)
            {
                var coordinates = new EntityCoordinates(trade, pad.Transform.LocalPosition);

                if (!FulfillOrder(order, account, coordinates, orderDatabase.PrinterOutput))
                    continue;

                tradeDestination = trade;
                order.NumDispatched++;
                if (order.OrderQuantity <= order.NumDispatched) //Spawn a crate on free pellets until the order is fulfilled.
                    break;
            }

            if (tradeDestination != null)
                break;
        }

        return tradeDestination;
    }

    /// <summary>
    /// Fulfills the specified cargo order and spawns paper attached to it.
    /// </summary>
    private bool FulfillOrder(
        CargoOrderData order,
        ProtoId<CargoAccountPrototype> account,
        EntityCoordinates spawn,
        string? paperProto
    )
    {
        if (!ProtoMan.Resolve(order.Product, out var product))
            return false;

        // Create the item itself
        var item = Spawn(product.Product, spawn);
        var itemXForm = Transform(item);

        // Ensure the item doesn't start anchored
        _transform.Unanchor(item, itemXForm);

        // Spawn container and insert the item into it if a container is defined.
        if (product.Container is { } productContainer)
        {
            var containerEntity = SpawnAttachedTo(productContainer.Entity, itemXForm.Coordinates);
            _transform.SetLocalRotation(containerEntity, itemXForm.LocalRotation);

            if (
                !_container.TryGetContainer(containerEntity, productContainer.ContainerId, out var container1)
                || !_container.Insert(item, container1, force: true)
            )
            {
                DebugTools.Assert(
                    $"Failed to insert cargo product into its specified container. This indicates an error in the cargo product definition's YAML as the product should be insertable into its container. {nameof(CargoProductPrototype)}: {(ProtoId<CargoProductPrototype>)order.Product.Id}"
                );
                QueueDel(containerEntity);
            }
            else
            {
                item = containerEntity;
            }
        }

        // Create a sheet of paper to write the order details on
        var printed = SpawnAttachedTo(paperProto, spawn);
        if (!_paperQuery.TryComp(printed, out var paper))
            return true;

        // fill in the order data
        var val = Loc.GetString("cargo-console-paper-print-name", ("orderNumber", order.OrderId));
        _metaSystem.SetEntityName(printed, val);

        var accountProto = ProtoMan.Index(account);
        _paperSystem.SetContent(
            (printed, paper),
            Loc.GetString(
                "cargo-console-paper-print-text",
                ("orderNumber", order.OrderId),
                ("itemName", product.Name),
                ("orderQuantity", order.OrderQuantity),
                ("requester", order.Requester),
                (
                    "reason",
                    string.IsNullOrWhiteSpace(order.Reason)
                        ? Loc.GetString("cargo-console-paper-reason-default")
                        : order.Reason
                ),
                ("account", Loc.GetString(accountProto.Name)),
                ("accountcode", Loc.GetString(accountProto.Code)),
                (
                    "approver",
                    string.IsNullOrWhiteSpace(order.Approver)
                        ? Loc.GetString("cargo-console-paper-approver-default")
                        : order.Approver
                )
            )
        );

        // attempt to attach the label to the item
        if (_paperLabelQuery.TryComp(item, out var label))
            _slots.TryInsert(item, label.LabelSlot, printed, null);

        return true;
    }

    public bool AddAndApproveOrder(
        EntityUid dbUid,
        string spawnId,
        string name,
        int cost,
        int qty,
        string sender,
        string? deliveryDestination, // CorvaxGoob-CargoFeatures
        string? note, // CorvaxGoob-CargoFeatures
        string dest,
        StationCargoOrderDatabaseComponent component,
        ProtoId<CargoAccountPrototype> account,
        Entity<StationDataComponent> stationData,
        bool securedDelivery = false // CorvaxGoob-CargoFeatures
    )
    {
        DebugTools.Assert(_protoMan.HasIndex<EntityPrototype>(spawnId));
        // Make an order
        var id = GenerateOrderId(component);
        // GoobStation - cooldown on Cargo Orders (specifically gamba)
        var order = new CargoOrderData(id, spawnId, name, cost, qty, sender, deliveryDestination, note, account, 0, securedDelivery); // CorvaxGoob-CargoFeatures

        // Approve it now
        order.SetApproverData(dest, sender);
        order.Approved = true;

        // Log order addition
        _adminLogger.Add(LogType.Action,
            LogImpact.Low,
            // CorvaxGoob-CargoFeatures : Рефактор старых названий
            $"AddAndApproveOrder {note} added order [orderId:{order.OrderId}, quantity:{order.OrderQuantity}, product:{order.ProductId}, requester:{order.Requester}, deliveryDestination:{order.DeliveryDestination}]");

        if (orderOut.NumDispatched >= orderOut.OrderQuantity)
        {
            // Order is complete. Remove from the queue.
            orderDb.Orders[account].RemoveAt(orderIdx);
        }
        return true;
    }

    private bool TryAddOrder(
        EntityUid dbUid,
        ProtoId<CargoAccountPrototype> account,
        CargoOrderData data,
        StationCargoOrderDatabaseComponent component
    )
    {
        component.Orders[account].Add(data);
        UpdateOrders(dbUid);
        return true;
    }

    private static int GenerateOrderId(StationCargoOrderDatabaseComponent orderDb)
    {
        // We need an arbitrary unique ID to identify orders, since they may
        // want to be cancelled later.
        return ++orderDb.NumOrdersCreated;
    }

    private void UpdateConsole()
    {
        var stationQuery = EntityQueryEnumerator<StationBankAccountComponent>();
        while (stationQuery.MoveNext(out var uid, out var bank))
        {
            if (Timing.CurTime < bank.NextIncomeTime)
                continue;
            bank.NextIncomeTime += bank.IncomeDelay;

            var balanceToAdd = (int)Math.Round(bank.IncreasePerSecond * bank.IncomeDelay.TotalSeconds);
            UpdateBankAccount((uid, bank), balanceToAdd, bank.RevenueDistribution);
        }
    }

    /// <summary>
    /// Updates all of the cargo-related consoles for a particular station.
    /// This should be called whenever orders change.
    /// </summary>
    private void UpdateOrders(EntityUid dbUid)
    {
        // Order added so all consoles need updating.
        var orderQuery = AllEntityQuery<CargoOrderConsoleComponent>();

        while (orderQuery.MoveNext(out var uid, out var _))
        {
            var station = _station.GetOwningStation(uid);
            if (station != dbUid)
                continue;

            UpdateOrderState(uid, station);
        }
    }

    private void UpdateOrderState(EntityUid consoleUid, EntityUid? station)
    {
        if (!_consoleQuery.TryComp(consoleUid, out var console))
            return;

        if (!_orderQuery.TryComp(station, out var orderDatabase))
            return;

        if (!_uiSystem.HasUi(consoleUid, CargoConsoleUiKey.Orders))
            return;

        _uiSystem.SetUiState(consoleUid,
            CargoConsoleUiKey.Orders,
            new CargoConsoleInterfaceState(
            MetaData(station.Value).EntityName,
            GetOutstandingOrderCount((station.Value, orderDatabase), console.Account),
            orderDatabase.Capacity,
            GetNetEntity(station.Value),
            RelevantOrders((station.Value, orderDatabase), (consoleUid, console)),
            GetAvailableProducts((consoleUid, console))
        ));
    }

    /// <summary>
    /// Gets orders relevant to this account, i.e. orders on the account directly or orders on behalf of the account in the primary account.
    /// </summary>
    /// CorvaxGoob-CargoFeatures : Новые аргументы функции для работы с оптовыми заказами
    private bool FulfillOrder(CargoOrderData order, ProtoId<CargoAccountPrototype> account, EntityCoordinates spawn, string? paperProto, out EntityUid? nextCrate, out List<string>? excessItemsOut, EntityUid? previousCrate = null, List<string>? excessItemsIn = null, bool addOrderContents = true)
    {
        if (!_bankQuery.TryComp(station, out var bank))
            return [];

        var ourOrders = station.Comp.Orders[console.Comp.Account];

        // Create a sheet of paper to write the order details on
        if (previousCrate is null) // // CorvaxGoob-CargoFeatures : Проверяем является ли это новым ящиком или старым
        {
            var printed = Spawn(paperProto, spawn);
            if (TryComp<PaperComponent>(printed, out var paper))
            {
                // CorvaxGoob-CargoFeatures-Start
                var itemName = productProto.Name;

                // fill in the order data
                var val = Loc.GetString("cargo-console-paper-print-name", ("orderNumber", order.OrderId), ("detailName", itemName), ("detailQuantity", order.OrderQuantity));
                // CorvaxGoob-CargoFeatures-End

                _metaSystem.SetEntityName(printed, val);

                // var accountProto = _protoMan.Index(account); CorvaxGoob-CargoFeatures : Перенесено повыше
                _paperSystem.SetContent((printed, paper),
                    Loc.GetString(
                        "cargo-console-paper-print-text",
                        ("orderNumber", order.OrderId),
                        ("itemName", itemName),
                        ("orderQuantity", order.OrderQuantity),
                        ("requester", order.Requester),
                        ("destination", string.IsNullOrWhiteSpace(order.DeliveryDestination) ? Loc.GetString("cargo-console-paper-delivery-destination-default") : order.DeliveryDestination), // CorvaxGoob-CargoFeatures
                        ("note", string.IsNullOrWhiteSpace(order.Note) ? Loc.GetString("cargo-console-paper-note-default") : order.Note), // CorvaxGoob-CargoFeatures
                        ("account", Loc.GetString(accountProto.Name)),
                        ("accountcode", Loc.GetString(accountProto.Code)),
                        ("approver", string.IsNullOrWhiteSpace(order.Approver) ? Loc.GetString("cargo-console-paper-approver-default") : order.Approver)));

                // attempt to attach the label to the item
                if (TryComp<PaperLabelComponent>(item, out var label))
                {
                    _slots.TryInsert(item.Value, label.LabelSlot, printed, null);
                }
            }
        }

        var otherOrders = station
            .Comp.Orders[bank.PrimaryAccount]
            .Where(order => order.Account == console.Comp.Account);

        return [.. ourOrders, .. otherOrders];
    }

    // CorvaxGoob-CargoFeatures : Перезагрузка метода для обращений без применения оптовой закупки
    private bool FulfillOrder(CargoOrderData order, ProtoId<CargoAccountPrototype> account, EntityCoordinates spawn, string? paperProto)
    {
        return FulfillOrder(order, account, spawn, paperProto, out _, out _);
    }


    // CorvaxGoob-CargoFeatures : Получение содержимого товара для объединения оптовых заказов
    private List<string>? GetBulkOrderContents(EntityPrototype productProto)
    {
        if (productProto.TryGetComponent<StorageFillComponent>(out var storageFill, Factory)) // удалить блок с удалением компонента
        {
            return EntitySpawnCollection.GetSpawns(storageFill.Contents, _random);
        }

        if (!productProto.TryGetComponent<EntityTableContainerFillComponent>(out var containerFill, Factory))
        {
            return null;
        }

        var containers = containerFill.Containers;

        if (containers.Count != 1
            || !containers.TryGetValue(
                SharedEntityStorageSystem.ContainerName,
                out var entityTable))
        {
            return null;
        }

        return _entityTable.GetSpawns(entityTable)
            .Select(prototype => prototype.Id)
            .ToList();
    }

    public List<ProtoId<CargoProductPrototype>> GetAvailableProducts(Entity<CargoOrderConsoleComponent> ent)
    {
        return _orderQuery.TryComp(stationUid, out dbComp);
    }

        // Note that a market must be both on the station and on the console to be available.
        var markets = ent.Comp.AllowedGroups.Intersect(db.Markets).ToList();
        var hiddenEv = new GetHiddenCargoProductsEvent(new HashSet<string>()); // Goobstation
        RaiseLocalEvent(ref hiddenEv); // Goobstation
        foreach (var product in _protoMan.EnumeratePrototypes<CargoProductPrototype>())
        {
            if (!_tradeStationQuery.HasComponent(gridUid))
                continue;

            if (hiddenEv.Hidden.Contains(product.ID)) // Goobstation
                continue; // Goobstation

            products.Add(product.ID);
        }
    }

    private static CargoOrderData GetOrderData(
        CargoConsoleAddOrderMessage args,
        CargoProductPrototype cargoProduct,
        int id,
        ProtoId<CargoAccountPrototype> account
    )
    {
        return new CargoOrderData(id, cargoProduct, args.Amount, args.Requester, args.Reason, account);
    }

    private void PlayDenySound(Entity<CargoOrderConsoleComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextDenySoundTime)
            return;

        ent.Comp.NextDenySoundTime = _timing.CurTime + ent.Comp.DenySoundDelay;
        _audio.PlayPvs(_audio.ResolveSound(ent.Comp.ErrorSound), ent.Owner);
    }
}

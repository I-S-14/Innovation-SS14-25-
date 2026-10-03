// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server._IS14.Economy;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared._IS14.Research;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Research;

/// <summary>
/// Department work orders: a head commissions a topic, and when the NIC delivers it the
/// department pays in its own data and out of its own budget.
/// </summary>
public sealed class IS14WorkOrderSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly StationBankAccountSystem _accounts = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>Account the payment lands in: the science budget, not a scientist's pocket.</summary>
    private const string ScienceAccount = "StationResearch";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14WorkOrdersComponent, IS14TechnologyUnlockedEvent>(OnTechnologyUnlocked);

        Subs.BuiEvents<IS14WorkOrderConsoleComponent>(IS14WorkOrderUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<IS14WorkOrderPlaceMessage>(OnPlace);
            subs.Event<IS14WorkOrderCancelMessage>(OnCancel);
        });
    }

    private void OnOpened(Entity<IS14WorkOrderConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPlace(Entity<IS14WorkOrderConsoleComponent> ent, ref IS14WorkOrderPlaceMessage args)
    {
        if (!this.IsPowered(ent.Owner, EntityManager) || !Allowed(ent, args.Actor))
            return;

        if (!_proto.TryIndex<TechnologyPrototype>(args.TechnologyId, out var tech))
            return;

        if (_points.ResolveStation(ent.Owner) is not { } station)
            return;

        EnsureComp<IS14WorkOrdersComponent>(station).Orders[ent.Comp.Department] = tech.ID;

        var identity = new TryGetIdentityShortInfoEvent(ent.Owner, args.Actor);
        RaiseLocalEvent(identity);

        if (ent.Comp.Channel is { } channel)
        {
            _radio.SendRadioMessage(
                ent.Owner,
                Loc.GetString("is14-workorder-placed-broadcast",
                    ("department", Loc.GetString(ent.Comp.DepartmentName)),
                    ("technology", Loc.GetString(tech.Name)),
                    ("signer", identity.Title ?? string.Empty)),
                channel,
                ent.Owner,
                escapeMarkup: false);
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} commissioned {tech.ID} for {ent.Comp.Department}.");

        UpdateUi(ent);
    }

    private void OnCancel(Entity<IS14WorkOrderConsoleComponent> ent, ref IS14WorkOrderCancelMessage args)
    {
        if (!this.IsPowered(ent.Owner, EntityManager) || !Allowed(ent, args.Actor))
            return;

        if (_points.ResolveStation(ent.Owner) is { } station
            && TryComp<IS14WorkOrdersComponent>(station, out var orders))
        {
            orders.Orders.Remove(ent.Comp.Department);
        }

        UpdateUi(ent);
    }

    private bool Allowed(Entity<IS14WorkOrderConsoleComponent> ent, EntityUid actor)
    {
        if (!TryComp<AccessReaderComponent>(ent.Owner, out var access)
            || _accessReader.IsAllowed(actor, ent.Owner, access))
        {
            return true;
        }

        _popup.PopupEntity(Loc.GetString("is14-workorder-no-access"), ent.Owner, actor);
        return false;
    }

    /// <summary>
    /// The NIC delivered something. Every department waiting for exactly that gets its order
    /// closed and pays up.
    /// </summary>
    private void OnTechnologyUnlocked(
        Entity<IS14WorkOrdersComponent> ent,
        ref IS14TechnologyUnlockedEvent args)
    {
        // Copied out of the ref event first: a ref parameter cannot be captured by a lambda.
        var technologyId = args.TechnologyId;

        var delivered = ent.Comp.Orders
            .Where(order => order.Value == technologyId)
            .Select(order => order.Key)
            .ToList();

        foreach (var department in delivered)
        {
            ent.Comp.Orders.Remove(department);
            Settle(ent.Owner, department, technologyId);
        }
    }

    /// <summary>
    /// Closes one order: data to the NIC, credits from the department's budget to the science
    /// budget, and a line on the radio so the station knows the trade happened.
    /// </summary>
    public bool Settle(EntityUid station, string department, string technologyId)
    {
        // The department's own terminal carries the rates, so that is also where a generous
        // head is configured.
        if (FindConsole(station, department) is not { } terminal)
            return false;

        var comp = terminal.Comp;

        if (comp.Bonus > 0)
            _points.AddPoints(station, comp.PointType, comp.Bonus);

        var paid = comp.Payment > 0
                   && _accounts.TryChangeStationBalance(station, comp.Account, -comp.Payment, out _)
                   && _accounts.TryChangeStationBalance(station, ScienceAccount, comp.Payment, out _);

        if (comp.Channel is { } channel && _proto.TryIndex<TechnologyPrototype>(technologyId, out var tech))
        {
            _radio.SendRadioMessage(
                terminal.Owner,
                Loc.GetString("is14-workorder-delivered-broadcast",
                    ("department", Loc.GetString(comp.DepartmentName)),
                    ("technology", Loc.GetString(tech.Name)),
                    ("bonus", comp.Bonus),
                    ("payment", paid ? comp.Payment : 0)),
                channel,
                terminal.Owner,
                escapeMarkup: false);
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"Work order of {department} for {technologyId} settled: {comp.Bonus} {comp.PointType.Id}, {(paid ? comp.Payment : 0)} credits.");

        return true;
    }

    /// <summary>A department's terminal on this station, if it still has one standing.</summary>
    private Entity<IS14WorkOrderConsoleComponent>? FindConsole(EntityUid station, string department)
    {
        var query = EntityQueryEnumerator<IS14WorkOrderConsoleComponent>();

        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Department == department && _points.ResolveStation(uid) == station)
                return (uid, console);
        }

        return null;
    }

    /// <summary>The order a department is waiting for, or null. Public for tests and quotas.</summary>
    public string? GetOrder(EntityUid? station, string department)
    {
        return station != null
               && TryComp<IS14WorkOrdersComponent>(station, out var orders)
               && orders.Orders.TryGetValue(department, out var tech)
            ? tech
            : null;
    }

    private void UpdateUi(Entity<IS14WorkOrderConsoleComponent> ent)
    {
        var state = new IS14WorkOrderUiState
        {
            Department = ent.Comp.Department,
            DepartmentName = Loc.GetString(ent.Comp.DepartmentName),
            Bonus = ent.Comp.Bonus,
            Payment = ent.Comp.Payment,
            PointType = ent.Comp.PointType.Id,
        };

        if (_points.ResolveStation(ent.Owner) is { } station)
        {
            state.Order = GetOrder(station, ent.Comp.Department);
            state.Budget = _accounts.GetStationAccount(station, ent.Comp.Account)?.Balance ?? 0;
        }

        if (_research.TryGetClientServer(ent.Owner, out var server, out _)
            && TryComp<TechnologyDatabaseComponent>(server, out var database))
        {
            foreach (var tech in new List<ProtoId<TechnologyPrototype>>(database.UnlockedTechnologies))
            {
                state.Unlocked.Add(tech.Id);
            }
        }

        foreach (var discipline in _proto.EnumeratePrototypes<TechDisciplinePrototype>())
        {
            if (discipline.ID.StartsWith("IS14"))
                state.Branches.Add(discipline.ID);
        }

        _ui.SetUiState(ent.Owner, IS14WorkOrderUiKey.Key, state);
    }
}

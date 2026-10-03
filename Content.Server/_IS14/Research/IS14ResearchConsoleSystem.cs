// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration.Logs;
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
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// Drives the NIC console: shows the five balances, sells technologies for them, and
/// hands the result to the upstream research server so lathes pick the recipes up.
/// </summary>
public sealed class IS14ResearchConsoleSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14ResearchCostSystem _costs = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly IS14ResearchRevealSystem _reveal = default!;
    [Dependency] private readonly IS14BreakthroughSystem _breakthroughs = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<IS14ResearchConsoleComponent>(IS14ResearchConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<IS14ResearchConsoleUnlockMessage>(OnUnlock);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<IS14ResearchConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, IS14ResearchConsoleUiKey.Key))
                UpdateUi(uid);
        }
    }

    private void OnOpened(Entity<IS14ResearchConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        // The console is an upstream research client: pull the server's database first so
        // the list of researched technologies is not a round old.
        _research.SyncClientWithServer(ent.Owner);
        UpdateUi(ent.Owner);
    }

    private void OnUnlock(Entity<IS14ResearchConsoleComponent> ent, ref IS14ResearchConsoleUnlockMessage args)
    {
        var actor = args.Actor;

        if (!this.IsPowered(ent.Owner, EntityManager))
            return;

        if (!_proto.TryIndex<TechnologyPrototype>(args.TechnologyId, out var tech))
            return;

        if (TryComp<AccessReaderComponent>(ent.Owner, out var access) && !_accessReader.IsAllowed(actor, ent.Owner, access))
        {
            _popup.PopupEntity(Loc.GetString("research-console-no-access-popup"), ent.Owner, actor);
            return;
        }

        if (!TryUnlock(ent, actor, tech, out var failReason))
        {
            if (failReason != null)
                _popup.PopupEntity(Loc.GetString(failReason), ent.Owner, actor);

            return;
        }

        UpdateUi(ent.Owner);
    }

    /// <summary>
    /// Buys a technology with the station's research data and files it with the research
    /// server. Public and free of UI so the whole purchase path can be tested.
    /// </summary>
    /// <param name="failReason">Loc id explaining the refusal, or null when it needs no words.</param>
    public bool TryUnlock(
        Entity<IS14ResearchConsoleComponent> console,
        EntityUid? actor,
        TechnologyPrototype tech,
        out string? failReason)
    {
        failReason = null;

        var station = _points.ResolveStation(console.Owner);

        // A hidden technology has to have been revealed for this station: a randomised upgrade
        // slot rolls one variant per round, and the rest do not exist at all.
        if (tech.Hidden && !_reveal.IsRevealed(station, tech.ID))
            return false;

        if (!_research.TryGetClientServer(console.Owner, out var server, out var serverComp)
            || !TryComp<TechnologyDatabaseComponent>(server, out var database))
        {
            failReason = "is14-research-console-no-server";
            return false;
        }

        if (_research.IsTechnologyUnlocked(server.Value, tech, database))
            return false;

        foreach (var prerequisite in tech.TechnologyPrerequisites)
        {
            if (_research.IsTechnologyUnlocked(server.Value, prerequisite.Id, database))
                continue;

            failReason = "is14-research-console-prereq";
            return false;
        }

        if (station is not { } payer)
        {
            failReason = "is14-research-no-station";
            return false;
        }

        // A breakthrough is not bought: it is paid for with an object that no longer exists.
        // No sample, no technology — however much data has piled up.
        if (_costs.GetBreakthrough(tech.ID) != null && !_breakthroughs.IsComplete(payer, tech.ID))
        {
            failReason = "is14-research-console-needs-sample";
            return false;
        }

        // Forks: taking one road closes the others for the rest of the shift.
        foreach (var sibling in _costs.GetExclusiveSiblings(tech.ID))
        {
            if (!_research.IsTechnologyUnlocked(server.Value, sibling, database))
                continue;

            failReason = "is14-research-console-excluded";
            return false;
        }

        // Samples taken apart in the destructive analyzer pay part of the price.
        var discount = GetDiscount(payer, tech.ID);
        var costs = IS14ResearchCostSystem.Discounted(_costs.GetCost(tech), discount);

        if (!_points.TrySpend(payer, costs))
        {
            failReason = "is14-research-console-cannot-afford";
            return false;
        }

        if (discount > 0f && TryComp<IS14ResearchDiscountComponent>(payer, out var discounts))
            discounts.Discounts.Remove(tech.ID);

        _research.AddTechnology(server.Value, tech);

        // Push the server's database to every client on it, so lathes, disk consoles and
        // the upstream console all see the new recipes immediately. Not every research
        // client keeps a database of its own (the AI upload console, for one), and syncing
        // those logs an error.
        foreach (var client in serverComp.Clients.ToList())
        {
            if (HasComp<TechnologyDatabaseComponent>(client))
                _research.Sync(client, server.Value);
        }

        ApplyEffects(console, payer, tech);
        Announce(console, actor, tech, costs);

        // Anything that was waiting for this technology — a department's order today, a
        // Gosplan quota tomorrow — hears about it here rather than being wired into the sale.
        var unlocked = new IS14TechnologyUnlockedEvent(payer, tech.ID, console.Owner);
        RaiseLocalEvent(payer, ref unlocked);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(actor):player} unlocked {tech.ID} at {ToPrettyString(console.Owner)} for {FormatCosts(costs)}.");

        return true;
    }

    /// <summary>
    /// Everything that cuts the price of a technology for this station: reverse-engineering
    /// credit from samples plus the shift's priority list, capped together.
    /// </summary>
    public float GetDiscount(EntityUid station, string technologyId)
    {
        var reverse = TryComp<IS14ResearchDiscountComponent>(station, out var discounts)
                      && discounts.Discounts.TryGetValue(technologyId, out var credit)
            ? credit
            : 0f;

        return MathF.Min(IS14ResearchCostSystem.MaxDiscount, reverse + _reveal.GetPriority(station, technologyId));
    }

    /// <summary>
    /// Runs everything the technology does besides unlocking recipes. Effects get a narrow
    /// context instead of the whole entity manager, so they stay data rather than code.
    /// </summary>
    private void ApplyEffects(EntityUid console, EntityUid station, TechnologyPrototype tech)
    {
        var effects = _costs.GetEffects(tech);

        if (effects.Count == 0)
            return;

        var context = new IS14TechEffectContext(
            station,
            console,
            Transform(console).Coordinates,
            (type, amount) => _points.AddPoints(station, type, amount),
            (modifier, delta) => _modifiers.AddModifier(station, modifier, delta),
            (proto, coords) => Spawn(proto, coords));

        foreach (var effect in effects)
        {
            effect.Apply(in context);
        }
    }

    private void Announce(
        Entity<IS14ResearchConsoleComponent> ent,
        EntityUid? actor,
        TechnologyPrototype tech,
        Dictionary<ProtoId<ResearchPointTypePrototype>, int> costs)
    {
        if (ent.Comp.AnnouncementChannel is not { } channel)
            return;

        var approver = string.Empty;

        if (actor != null)
        {
            var identity = new TryGetIdentityShortInfoEvent(ent.Owner, actor.Value);
            RaiseLocalEvent(identity);
            approver = identity.Title ?? string.Empty;
        }

        _radio.SendRadioMessage(
            ent.Owner,
            Loc.GetString("is14-research-console-unlocked-broadcast",
                ("technology", Loc.GetString(tech.Name)),
                ("cost", FormatCosts(costs)),
                ("approver", approver)),
            channel,
            ent.Owner,
            escapeMarkup: false);
    }

    /// <summary>"25 НАУ, 40 ВОЕН" — what the radio and the logs quote.</summary>
    private string FormatCosts(Dictionary<ProtoId<ResearchPointTypePrototype>, int> costs)
    {
        var parts = new List<string>();

        foreach (var (type, amount) in costs.OrderBy(pair => pair.Key.Id))
        {
            var name = _proto.TryIndex(type, out ResearchPointTypePrototype? proto)
                ? Loc.GetString(proto.ShortName)
                : type.Id;

            parts.Add($"{amount} {name}");
        }

        return string.Join(", ", parts);
    }

    private void UpdateUi(EntityUid console)
    {
        var state = new IS14ResearchConsoleUiState
        {
            IncomeWindowMinutes = (int) IS14ResearchPointSystem.IncomeWindow.TotalMinutes,
        };

        if (TryComp<IS14ResearchConsoleComponent>(console, out var consoleComp))
        {
            foreach (var branch in consoleComp.Branches)
            {
                state.Branches.Add(branch.Id);
            }
        }

        if (_points.ResolveStation(console) is { } station)
        {
            state.Points = _points.GetAllPoints(station);
            state.RecentIncome = _points.GetRecentIncome(station);

            if (TryComp<IS14ResearchDiscountComponent>(station, out var discounts))
                state.Discounts = new Dictionary<string, float>(discounts.Discounts);

            if (TryComp<IS14ResearchPrioritiesComponent>(station, out var priorities))
                state.Priorities = new Dictionary<string, float>(priorities.Priorities);

            if (TryComp<IS14RevealedTechComponent>(station, out var revealed))
                state.Revealed = new List<string>(revealed.Revealed);

            state.Breakthroughs = _breakthroughs.Completed(station);
        }

        if (_research.TryGetClientServer(console, out var server, out var serverComp)
            && TryComp<TechnologyDatabaseComponent>(server, out var database))
        {
            state.ServerConnected = true;
            state.ServerName = serverComp.ServerName;

            // Copied rather than projected: the upstream component only grants read access,
            // so calling anything on the list itself is a compile error.
            var unlocked = new List<ProtoId<TechnologyPrototype>>(database.UnlockedTechnologies);

            foreach (var tech in unlocked)
            {
                state.Unlocked.Add(tech.Id);
            }
        }

        _ui.SetUiState(console, IS14ResearchConsoleUiKey.Key, state);
    }
}

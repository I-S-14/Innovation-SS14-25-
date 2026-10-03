// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._IS14.Research;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Research.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// The First Department's bench: confiscated hardware goes in, and a line of research that was
/// not on the map comes out.
/// </summary>
/// <remarks>
/// Revealing is not researching. The bench opens the topic for the station's consoles — it
/// still has to be bought with data afterwards, and it is deliberately expensive. What the
/// bench actually creates is a reason for security to hand the confiscated e-sword to science
/// instead of burying it in evidence.
/// </remarks>
public sealed class IS14ReverseEngineeringSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly IS14ResearchRevealSystem _reveal = default!;
    [Dependency] private readonly IS14ResearchCostSystem _costs = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14ReverseEngineeringBenchComponent, InteractUsingEvent>(OnInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IS14ReverseEngineeringBenchComponent>();
        while (query.MoveNext(out var uid, out var bench))
        {
            if (bench.FinishesAt is not { } finish || _timing.CurTime < finish)
                continue;

            Finish((uid, bench));
        }
    }

    private void OnInteractUsing(
        Entity<IS14ReverseEngineeringBenchComponent> ent,
        ref InteractUsingEvent args)
    {
        if (args.Handled || !this.IsPowered(ent.Owner, EntityManager))
            return;

        if (_whitelist.IsWhitelistFail(ent.Comp.Whitelist, args.Used)
            || _whitelist.IsWhitelistPass(ent.Comp.Blacklist, args.Used))
        {
            return;
        }

        args.Handled = true;

        TryReverseEngineer(ent, args.Used, args.User);
    }

    /// <summary>
    /// Takes a sample apart. Public and free of the interaction plumbing so the path can be
    /// tested; returns false when the bench refuses, with the reason already popped up.
    /// </summary>
    public bool TryReverseEngineer(
        Entity<IS14ReverseEngineeringBenchComponent> ent,
        EntityUid sample,
        EntityUid? user)
    {
        if (ent.Comp.PendingTechnology != null)
        {
            Tell(ent, user, "is14-research-bench-busy");
            return false;
        }

        var station = _points.ResolveStation(ent.Owner);
        var prototypeId = MetaData(sample).EntityPrototype?.ID;

        if (!TryMatch(station, prototypeId, out var technologyId))
        {
            Tell(ent, user, "is14-reverse-nothing-to-learn");
            return false;
        }

        ent.Comp.PendingTechnology = technologyId;
        ent.Comp.PendingSanctioned = user == null || HasSanction(ent, user.Value);
        ent.Comp.User = user;
        ent.Comp.FinishesAt = _timing.CurTime + ent.Comp.Duration;

        QueueDel(sample);

        _audio.PlayPvs(ent.Comp.StartSound, ent.Owner);
        Tell(ent, user, "is14-reverse-started");

        return true;
    }

    /// <summary>
    /// A hidden technology this station has not uncovered yet whose contraband requirement the
    /// sample satisfies.
    /// </summary>
    public bool TryMatch(EntityUid? station, string? samplePrototype, out string technologyId)
    {
        technologyId = string.Empty;

        if (string.IsNullOrEmpty(samplePrototype))
            return false;

        foreach (var data in _proto.EnumeratePrototypes<IS14TechDataPrototype>())
        {
            if (data.Contraband is not { } contraband || _reveal.IsRevealed(station, data.ID))
                continue;

            if (!IS14PrototypeKin.MatchesAny(_proto, samplePrototype, contraband.Samples))
                continue;

            technologyId = data.ID;
            return true;
        }

        return false;
    }

    private void Finish(Entity<IS14ReverseEngineeringBenchComponent> ent)
    {
        var technologyId = ent.Comp.PendingTechnology;
        var sanctioned = ent.Comp.PendingSanctioned;
        var user = ent.Comp.User;

        ent.Comp.PendingTechnology = null;
        ent.Comp.User = null;
        ent.Comp.FinishesAt = null;

        if (technologyId == null
            || _points.ResolveStation(ent.Owner) is not { } station
            || !_proto.TryIndex<TechnologyPrototype>(technologyId, out var tech))
        {
            return;
        }

        _reveal.Reveal(station, technologyId);

        if (_costs.GetData(technologyId)?.Contraband is { Payout: > 0 } requirement)
        {
            var payout = Math.Max(1, (int) MathF.Round(
                requirement.Payout
                * (sanctioned ? 1f : ent.Comp.UnsanctionedMultiplier)
                * _points.PayoutMultiplier
                * _modifiers.GetPayoutMultiplier(station, requirement.PointType)));

            _points.AddPoints(station, requirement.PointType, payout);
        }

        var message = Loc.GetString("is14-reverse-revealed", ("technology", Loc.GetString(tech.Name)));

        if (ent.Comp.Channel is { } channel)
            _radio.SendRadioMessage(ent.Owner, message, channel, ent.Owner, escapeMarkup: false);

        if (user != null)
            _popup.PopupEntity(message, ent.Owner, user.Value);

        // One interpolated literal, because the admin log takes an interpolated string
        // handler and a concatenation is not one.
        _adminLog.Add(LogType.Action, sanctioned ? LogImpact.Medium : LogImpact.High,
            $"{ToPrettyString(user):player} reverse engineered contraband into {technologyId}, sanctioned: {sanctioned}.");
    }

    /// <summary>A sanction form with every required stamp, carried by the operator.</summary>
    public bool HasSanction(Entity<IS14ReverseEngineeringBenchComponent> ent, EntityUid user)
    {
        if (ent.Comp.SanctionStamps.Count == 0)
            return true;

        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            if (!HasComp<IS14SanctionFormComponent>(item)
                || !TryComp<PaperComponent>(item, out var paper))
            {
                continue;
            }

            var complete = true;

            foreach (var required in ent.Comp.SanctionStamps)
            {
                var found = false;

                foreach (var stamp in paper.StampedBy)
                {
                    if (stamp.StampedName != required)
                        continue;

                    found = true;
                    break;
                }

                if (found)
                    continue;

                complete = false;
                break;
            }

            if (complete)
                return true;
        }

        return false;
    }

    private void Tell(Entity<IS14ReverseEngineeringBenchComponent> ent, EntityUid? user, string message)
    {
        if (user != null)
            _popup.PopupEntity(Loc.GetString(message), ent.Owner, user.Value);
    }
}

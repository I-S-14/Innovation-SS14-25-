// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server._IS14.Economy;
using Content.Server.Paper;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._IS14.Research;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// Publications: a paper about a result, stamped by the people who answer for it, handed in
/// for credits and a little data.
/// </summary>
public sealed class IS14PublicationSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly StationBankAccountSystem _accounts = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14PublicationsComponent, IS14TechnologyUnlockedEvent>(OnUnlocked);
        SubscribeLocalEvent<IS14PublicationPrinterComponent, InteractHandEvent>(OnPrinterUsed);
        SubscribeLocalEvent<IS14PublicationReceiverComponent, InteractUsingEvent>(OnPaperHandedIn);
    }

    /// <summary>
    /// Remembers what was researched last, because that is what there is to write about.
    /// </summary>
    private void OnUnlocked(Entity<IS14PublicationsComponent> ent, ref IS14TechnologyUnlockedEvent args)
    {
        ent.Comp.LastUnlocked = args.TechnologyId;
    }

    private void OnPrinterUsed(Entity<IS14PublicationPrinterComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !this.IsPowered(ent.Owner, EntityManager))
            return;

        args.Handled = true;

        if (_timing.CurTime < ent.Comp.NextReady)
        {
            _popup.PopupEntity(Loc.GetString("is14-research-printer-busy"), ent.Owner, args.User);
            return;
        }

        if (_points.ResolveStation(ent.Owner) is not { } station
            || !TryComp<IS14PublicationsComponent>(station, out var publications)
            || publications.LastUnlocked is not { } technologyId
            || !_proto.TryIndex<TechnologyPrototype>(technologyId, out var tech))
        {
            _popup.PopupEntity(Loc.GetString("is14-publication-nothing-to-report"), ent.Owner, args.User);
            return;
        }

        if (publications.Filed.Contains(technologyId))
        {
            _popup.PopupEntity(Loc.GetString("is14-publication-already-filed"), ent.Owner, args.User);
            return;
        }

        ent.Comp.NextReady = _timing.CurTime + ent.Comp.Cooldown;

        var paper = Spawn(ent.Comp.PaperPrototype, Transform(ent.Owner).Coordinates);

        EnsureComp<IS14PublicationComponent>(paper).TechnologyId = technologyId;

        if (TryComp<PaperComponent>(paper, out var paperComp))
        {
            _paper.SetContent((paper, paperComp), Loc.GetString("is14-publication-body",
                ("technology", Loc.GetString(tech.Name)),
                ("tier", tech.Tier)));
        }

        _hands.TryPickupAnyHand(args.User, paper);
        _audio.PlayPvs(ent.Comp.Sound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("is14-publication-printed"), ent.Owner, args.User);
    }

    private void OnPaperHandedIn(Entity<IS14PublicationReceiverComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !this.IsPowered(ent.Owner, EntityManager))
            return;

        if (!TryComp<IS14PublicationComponent>(args.Used, out var publication))
            return;

        args.Handled = true;

        File((ent.Owner, ent.Comp), (args.Used, publication), args.User);
    }

    /// <summary>
    /// Hands one paper in. Public and free of the interaction plumbing so the whole path can
    /// be tested: stamps, payment, and the one-paper-per-result rule.
    /// </summary>
    public bool File(
        Entity<IS14PublicationReceiverComponent> receiver,
        Entity<IS14PublicationComponent> paper,
        EntityUid? user)
    {
        if (paper.Comp.Filed)
        {
            Refuse(receiver, user, "is14-publication-already-filed");
            return false;
        }

        if (!_proto.TryIndex<TechnologyPrototype>(paper.Comp.TechnologyId, out var tech))
        {
            Refuse(receiver, user, "is14-publication-not-a-paper");
            return false;
        }

        if (MissingStamp(receiver, paper) is { } missing)
        {
            Refuse(receiver, user, "is14-publication-needs-stamp", ("stamp", Loc.GetString(missing)));
            return false;
        }

        if (_points.ResolveStation(receiver.Owner) is not { } station)
        {
            Refuse(receiver, user, "is14-research-no-station");
            return false;
        }

        var publications = EnsureComp<IS14PublicationsComponent>(station);

        if (!publications.Filed.Add(tech.ID))
        {
            Refuse(receiver, user, "is14-publication-already-filed");
            return false;
        }

        paper.Comp.Filed = true;

        // A paper about a harder result is worth more, and the tier is the only measure of
        // that we already have.
        var payout = Math.Max(1, (int) MathF.Round(
            receiver.Comp.BaseValue
            * tech.Tier
            * _points.PayoutMultiplier
            * _modifiers.GetPayoutMultiplier(station, receiver.Comp.PointType)));

        _points.AddPoints(station, receiver.Comp.PointType, payout);

        var payment = receiver.Comp.Payment * tech.Tier;
        var paid = payment > 0
                   && _accounts.TryChangeStationBalance(station, receiver.Comp.Account, payment, out _);

        _audio.PlayPvs(receiver.Comp.Sound, receiver.Owner);
        QueueDel(paper.Owner);

        if (receiver.Comp.Channel is { } channel)
        {
            _radio.SendRadioMessage(
                receiver.Owner,
                Loc.GetString("is14-publication-accepted-broadcast",
                    ("technology", Loc.GetString(tech.Name)),
                    ("amount", payout),
                    ("payment", paid ? payment : 0)),
                channel,
                receiver.Owner,
                escapeMarkup: false);
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} published {tech.ID}: {payout} data, {(paid ? payment : 0)} credits.");

        return true;
    }

    /// <summary>The first required stamp the paper does not carry, or null when it is in order.</summary>
    private string? MissingStamp(
        Entity<IS14PublicationReceiverComponent> receiver,
        Entity<IS14PublicationComponent> paper)
    {
        if (!TryComp<PaperComponent>(paper.Owner, out var paperComp))
            return receiver.Comp.RequiredStamps.Count > 0 ? receiver.Comp.RequiredStamps[0] : null;

        foreach (var required in receiver.Comp.RequiredStamps)
        {
            var found = false;

            foreach (var stamp in paperComp.StampedBy)
            {
                if (stamp.StampedName != required)
                    continue;

                found = true;
                break;
            }

            if (!found)
                return required;
        }

        return null;
    }

    private void Refuse(
        Entity<IS14PublicationReceiverComponent> receiver,
        EntityUid? user,
        string reason,
        params (string, object)[] args)
    {
        if (user == null)
            return;

        _popup.PopupEntity(
            args.Length == 0 ? Loc.GetString(reason) : Loc.GetString(reason, args),
            receiver.Owner,
            user.Value);
    }
}

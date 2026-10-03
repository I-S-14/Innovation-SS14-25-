// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server._IS14.Economy;
using Content.Shared._IS14.Research;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio.Systems;

namespace Content.Server._IS14.Research;

/// <summary>
/// Volunteers: consent on paper, data from the living, and credits out of the department's
/// budget into the volunteer's pocket.
/// </summary>
/// <remarks>
/// The unconsented path is deliberately left open. A scientist who examines an unwilling
/// subject gets poor data and a line in the admin log with both names on it — evidence
/// security can act on, and a story, rather than a refusal message.
/// </remarks>
public sealed class IS14VolunteerSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly IS14ExperimentSystem _experiment = default!;
    [Dependency] private readonly BankingSystem _banking = default!;
    [Dependency] private readonly StationBankAccountSystem _accounts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14ConsentFormComponent, UseInHandEvent>(OnFormUse);
        SubscribeLocalEvent<IS14ConsentFormComponent, IS14ConsentSignDoAfterEvent>(OnFormSigned);
        SubscribeLocalEvent<IS14ConsentFormComponent, ExaminedEvent>(OnFormExamined);
        SubscribeLocalEvent<IS14VolunteerScannerComponent, AfterInteractEvent>(OnScannerUsed);
        SubscribeLocalEvent<IS14VolunteerScannerComponent, IS14VolunteerScanDoAfterEvent>(OnScanFinished);
    }

    private void OnFormUse(Entity<IS14ConsentFormComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Signed)
        {
            _popup.PopupEntity(Loc.GetString("is14-consent-already-signed"), ent.Owner, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager, args.User, ent.Comp.SignTime, new IS14ConsentSignDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("is14-consent-signing"), ent.Owner, args.User);
    }

    private void OnFormSigned(Entity<IS14ConsentFormComponent> ent, ref IS14ConsentSignDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Signed)
            return;

        args.Handled = true;

        Sign(ent, args.User);
    }

    /// <summary>Signs the form in someone's name. Public so the path is testable.</summary>
    public void Sign(Entity<IS14ConsentFormComponent> ent, EntityUid subject)
    {
        ent.Comp.Signed = true;
        ent.Comp.SubjectName = Identity.Name(subject, EntityManager);
        ent.Comp.SubjectKey = SubjectKey(subject);

        if (_mind.TryGetMind(subject, out var mindId, out _)
            && _job.MindTryGetJobName(mindId, out var jobName))
        {
            ent.Comp.SubjectTitle = jobName;
        }

        _popup.PopupEntity(Loc.GetString("is14-consent-signed"), ent.Owner, subject);
    }

    private void OnFormExamined(Entity<IS14ConsentFormComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(ent.Comp.Signed
            ? Loc.GetString("is14-consent-examine-signed",
                ("name", ent.Comp.SubjectName),
                ("title", ent.Comp.SubjectTitle))
            : Loc.GetString("is14-consent-examine-blank"));
    }

    private void OnScannerUsed(Entity<IS14VolunteerScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        if (!HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;

        if (_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("is14-research-scanner-needs-alive"), ent.Owner, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager, args.User, ent.Comp.Delay, new IS14VolunteerScanDoAfterEvent(), ent.Owner, target, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("is14-volunteer-scan-start"), ent.Owner, args.User);
    }

    private void OnScanFinished(Entity<IS14VolunteerScannerComponent> ent, ref IS14VolunteerScanDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;

        Examine(ent, args.User, target);
    }

    /// <summary>
    /// One examination of a living subject. Public and free of do-afters so the whole path —
    /// consent, payout, fee — can be tested.
    /// </summary>
    public IS14MeasurementResult Examine(
        Entity<IS14VolunteerScannerComponent> ent,
        EntityUid user,
        EntityUid subject)
    {
        if (_points.ResolveStation(ent.Owner) is not { } station)
            return IS14MeasurementResult.Refuse("is14-research-no-station");

        var key = SubjectKey(subject);
        var consented = HasConsent(user, key) || HasConsent(subject, key);

        // One person is one measurement: examining the same volunteer all shift is a repeat.
        var prior = _points.RecordMeasurement(station, $"{ent.Comp.ProfileNamespace}:{key}");

        var payout = Math.Max(1, (int) MathF.Round(
            ent.Comp.BaseValue
            * (consented ? 1f : ent.Comp.UnconsentedMultiplier)
            * _points.PayoutMultiplier
            * _modifiers.GetPayoutMultiplier(station, ent.Comp.PointType)
            * _experiment.NoveltyMultiplier(station, prior)));

        _points.AddPoints(station, ent.Comp.PointType, payout);

        var result = new IS14MeasurementResult
        {
            Payout = payout,
            PointType = ent.Comp.PointType,
            PriorCount = prior,
            ProfileKey = key,
        };

        if (consented)
        {
            Pay(ent, station, subject);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("is14-volunteer-no-consent"), ent.Owner, subject);

            _adminLog.Add(LogType.Action, LogImpact.High,
                $"{ToPrettyString(user):player} examined {ToPrettyString(subject):target} with {ToPrettyString(ent.Owner)} without consent.");
        }

        _audio.PlayPvs(ent.Comp.Sound, ent.Owner);
        _experiment.ReportResult(ent.Owner, user, result, ent.Comp.Channel);

        return result;
    }

    /// <summary>Pays the volunteer out of the department's budget. No budget, no fee.</summary>
    private void Pay(Entity<IS14VolunteerScannerComponent> ent, EntityUid station, EntityUid subject)
    {
        if (ent.Comp.Fee <= 0)
            return;

        if (!_accounts.TryChangeStationBalance(station, ent.Comp.Account, -ent.Comp.Fee, out _))
        {
            _popup.PopupEntity(Loc.GetString("is14-volunteer-no-funds"), ent.Owner, subject);
            return;
        }

        if (!_banking.TryChangeBalance(subject, ent.Comp.Fee, out _,
                Loc.GetString("is14-volunteer-payment-description"), ent.Owner))
        {
            // Nothing to pay into: the money goes back rather than vanishing.
            _accounts.TryChangeStationBalance(station, ent.Comp.Account, ent.Comp.Fee, out _);
            return;
        }

        _popup.PopupEntity(Loc.GetString("is14-volunteer-paid", ("amount", ent.Comp.Fee)), ent.Owner, subject);
    }

    /// <summary>A signed form for this subject, in the holder's hands or on their person.</summary>
    public bool HasConsent(EntityUid holder, string subjectKey)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(holder))
        {
            if (TryComp<IS14ConsentFormComponent>(item, out var form)
                && form.Signed
                && form.SubjectKey == subjectKey)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The person behind the body, so a form cannot be reused for someone else.</summary>
    public string SubjectKey(EntityUid subject)
    {
        if (!_mind.TryGetMind(subject, out var mindId, out var mind))
            return $"nomind:{subject}";

        return mind.UserId?.ToString() ?? mindId.ToString();
    }
}

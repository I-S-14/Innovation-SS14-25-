// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.EntitySystems;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Research;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// Sociology questionnaires: the social data loop. A form is only worth anything once a
/// real crew member has filled it in, which is why this currency cannot be scripted.
/// </summary>
public sealed class IS14SurveySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Salary that maps to weight 1.0 — the flat Soviet pay scale starts here.</summary>
    private const float BaseSalary = 200f;

    private const float MaxWeight = 2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14SurveyPrinterComponent, InteractHandEvent>(OnPrinterInteract);
        SubscribeLocalEvent<IS14SurveyFormComponent, UseInHandEvent>(OnFormUse);
        SubscribeLocalEvent<IS14SurveyFormComponent, IS14SurveyFillDoAfterEvent>(OnFormFilled);
        SubscribeLocalEvent<IS14SurveyFormComponent, IS14GetExperimentProfileEvent>(OnFormProfile);
        SubscribeLocalEvent<IS14SurveyFormComponent, ExaminedEvent>(OnFormExamined);
    }

    private void OnPrinterInteract(Entity<IS14SurveyPrinterComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !this.IsPowered(ent.Owner, EntityManager))
            return;

        args.Handled = true;

        if (_timing.CurTime < ent.Comp.NextReady)
        {
            _popup.PopupEntity(Loc.GetString("is14-research-printer-busy"), ent.Owner, args.User);
            return;
        }

        ent.Comp.NextReady = _timing.CurTime + ent.Comp.Cooldown;

        var form = Spawn(ent.Comp.FormPrototype, Transform(ent.Owner).Coordinates);
        _hands.TryPickupAnyHand(args.User, form);
        _audio.PlayPvs(ent.Comp.Sound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("is14-research-printer-printed"), ent.Owner, args.User);
    }

    private void OnFormUse(Entity<IS14SurveyFormComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Filled)
        {
            _popup.PopupEntity(Loc.GetString("is14-survey-already-filled"), ent.Owner, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.FillTime, new IS14SurveyFillDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("is14-survey-filling"), ent.Owner, args.User);
    }

    private void OnFormFilled(Entity<IS14SurveyFormComponent> ent, ref IS14SurveyFillDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Filled)
            return;

        args.Handled = true;

        var user = args.User;

        ent.Comp.Filled = true;
        ent.Comp.RespondentName = Identity.Name(user, EntityManager);

        // One person, one useful survey: the key is the player behind the body, so a
        // respondent cannot be milked by handing them a second form.
        if (_mind.TryGetMind(user, out var mindId, out var mind))
        {
            ent.Comp.RespondentKey = mind.UserId?.ToString() ?? mindId.ToString();

            if (_job.MindTryGetJobId(mindId, out var jobId) && jobId != null)
            {
                ent.Comp.RespondentTitle = _job.MindTryGetJobName(mindId, out var jobName)
                    ? jobName
                    : jobId.Value.Id;

                ent.Comp.Weight = WeightForJob(jobId.Value);
            }
        }
        else
        {
            // No mind means nobody actually answered anything.
            ent.Comp.RespondentKey = $"nomind:{user}";
            ent.Comp.Weight = 0.2f;
        }

        _popup.PopupEntity(Loc.GetString("is14-survey-filled"), ent.Owner, user);
    }

    /// <summary>
    /// A filled form describes itself to any bench: the archive remembers the respondent,
    /// the payout follows their position.
    /// </summary>
    private void OnFormProfile(Entity<IS14SurveyFormComponent> ent, ref IS14GetExperimentProfileEvent args)
    {
        if (!ent.Comp.Filled)
        {
            args.Refuse = true;
            args.RefuseReason = "is14-survey-not-filled";
            return;
        }

        args.ProfileKey = $"respondent:{ent.Comp.RespondentKey}";
        args.Multiplier = ent.Comp.Weight;
    }

    private void OnFormExamined(Entity<IS14SurveyFormComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(ent.Comp.Filled
            ? Loc.GetString("is14-survey-examine-filled",
                ("name", ent.Comp.RespondentName),
                ("title", ent.Comp.RespondentTitle))
            : Loc.GetString("is14-survey-examine-blank"));
    }

    /// <summary>
    /// Weight from the pay scale, reusing the economy's job table: a captain's answers are
    /// worth twice an assistant's, and nothing needs a second table to maintain.
    /// </summary>
    private float WeightForJob(ProtoId<Content.Shared.Roles.JobPrototype> job)
    {
        if (!_proto.TryIndex<JobEconomyPrototype>(job.Id, out var economy) || economy.Salary <= 0)
            return 1f;

        return Math.Clamp(economy.Salary / BaseSalary, 1f, MaxWeight);
    }
}

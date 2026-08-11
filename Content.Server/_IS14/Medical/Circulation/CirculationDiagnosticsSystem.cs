// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.Circulation;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server._IS14.Medical.Circulation;

/// <summary>
/// Reading a patient without a machine.
/// </summary>
/// <remarks>
/// The metrics exist so a doctor can act on them, and a doctor who has to fetch an analyser
/// before knowing whether somebody is dying is a doctor who arrives late. Two channels here:
/// what anybody sees at a glance, and what anybody can find out with two fingers and three
/// seconds. Neither needs an item, which is the point — triage should not be gated behind
/// equipment.
/// </remarks>
public sealed class CirculationDiagnosticsSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly TimeSpan PulseDelay = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CirculationComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CirculationComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CirculationComponent, PulseCheckDoAfterEvent>(OnPulseChecked);
    }

    /// <summary>
    /// What the body says about itself before anybody touches it.
    /// </summary>
    /// <remarks>
    /// Deliberately without numbers and without a stage name. "Grey and sweating" is a thing
    /// a passer-by can report over the radio; "decompensated, delivery 0.7" is not.
    /// </remarks>
    private void OnExamined(Entity<CirculationComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || _mobState.IsDead(ent))
            return;

        var target = Identity.Entity(ent, EntityManager);

        var line = ent.Comp.Stage switch
        {
            ShockStage.Compensating => "is14-circulation-examine-pale",
            ShockStage.Decompensating => "is14-circulation-examine-grey",
            ShockStage.Shock => "is14-circulation-examine-cyanotic",
            _ => null,
        };

        if (line != null)
            args.PushMarkup(Loc.GetString(line, ("target", target)));

        if (ent.Comp.Collapsed)
            args.PushMarkup(Loc.GetString("is14-circulation-examine-collapsed", ("target", target)));
    }

    private void OnGetVerbs(Entity<CirculationComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("is14-circulation-verb-pulse"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager,
                user,
                PulseDelay,
                new PulseCheckDoAfterEvent(),
                ent.Owner,
                ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            }),
        });
    }

    /// <summary>
    /// Reports the rate in words rather than numbers.
    /// </summary>
    /// <remarks>
    /// A fast, weak pulse on somebody who is standing and talking is the whole diagnosis: it
    /// says they have lost a third of their blood and are being held together by their heart.
    /// Reading that is a skill; the words are chosen so that it can be learned.
    /// </remarks>
    private void OnPulseChecked(Entity<CirculationComponent> ent, ref PulseCheckDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var target = Identity.Entity(ent, EntityManager);
        var comp = ent.Comp;

        // Weakness is a fact about the pump, not the rate: plenty of fluid moving fast reads
        // strong, and the same rate with nothing behind it is what "thready" means.
        var weak = comp.Volume < 0.75f;

        var line = (comp.HeartRate, weak) switch
        {
            (<= 1f, _) => "is14-circulation-pulse-absent",
            (< 60f, _) => "is14-circulation-pulse-slow",
            (< 100f, false) => "is14-circulation-pulse-calm",
            (< 100f, true) => "is14-circulation-pulse-weak",
            (< 140f, false) => "is14-circulation-pulse-fast",
            (< 140f, true) => "is14-circulation-pulse-fast-weak",
            _ => "is14-circulation-pulse-thready",
        };

        _popup.PopupEntity(Loc.GetString(line, ("target", target)), ent, args.User);
    }
}

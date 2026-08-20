// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Shared._IS14.Medical.Circulation;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.Circulation;

/// <summary>
/// The oxygen delivery loop: measure the three multipliers, let the heart try to cover the
/// shortfall, and punish whatever it could not cover.
/// </summary>
/// <remarks>
/// Server-side because saturation is read off the upstream respirator, which is a server
/// component. The metrics themselves are networked on <see cref="CirculationComponent"/>, so
/// the alert, the pulse verb and the analyser all read the same numbers this produced.
/// <para>
/// Nothing here knows what a disease is. The heart's ceiling and the body's demand are both
/// asked for by event, so heart disease, fever, cold and sedation can all lean on this
/// without it ever growing a reference to them.
/// </para>
/// </remarks>
public sealed class CirculationSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    /// <summary>
    /// One second. Fine enough that a racing heart visibly climbs rather than jumping, coarse
    /// enough that it costs nothing.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private static readonly EntProtoId Woozy = "StatusEffectWoozy";
    private static readonly EntProtoId Stutter = "StatusEffectStutter";
    private static readonly EntProtoId Bloodloss = "StatusEffectBloodloss";
    /// <summary>Обморок: сбитый с ног и неспособный действовать, но не спящий.</summary>
    private static readonly EntProtoId Collapse = "IS14StatusEffectCollapse";

    /// <summary>
    /// Symptoms are refreshed every tick with a lifetime slightly longer than the tick, so
    /// they lapse on their own the moment the condition stops being true. Nothing has to
    /// remember to take them off.
    /// </summary>
    private static readonly TimeSpan SymptomDuration = TimeSpan.FromSeconds(2.5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CirculationComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CirculationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.HeartRate = ent.Comp.RestingRate;
        ent.Comp.Ceiling = ent.Comp.MaxRate;
        ent.Comp.NextUpdate = _timing.CurTime + Interval;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CirculationComponent, BloodstreamComponent>();

        while (query.MoveNext(out var uid, out var comp, out var bloodstream))
        {
            if (now < comp.NextUpdate)
                continue;

            var dt = (float) Interval.TotalSeconds;
            comp.NextUpdate = now + Interval;

            // A corpse has no circulation to model. Bleeding still happens upstream.
            if (_mobState.IsDead(uid))
                continue;

            Step((uid, comp), bloodstream, dt);
        }
    }

    private void Step(Entity<CirculationComponent> ent, BloodstreamComponent bloodstream, float dt)
    {
        var comp = ent.Comp;

        Measure(ent, bloodstream);

        // Floored at zero rather than at the resting rate, because a ceiling of zero is how a
        // stopped heart is expressed — one knob covering everything from healthy to arrest.
        var ceilingEv = new GetHeartCeilingEvent(comp.MaxRate);
        RaiseLocalEvent(ent, ref ceilingEv);
        var ceiling = Math.Clamp(ceilingEv.Ceiling, 0f, comp.MaxRate);

        // The heart runs on the same blood it pumps. Last tick's delivery is what it had to
        // work with, so a failing circulation drags its own pump down with it — which is why a
        // patient bled white slows down instead of racing.
        var span = comp.CoronaryComfort - comp.CoronaryFloor;
        var supply = span <= 0f
            ? 1f
            : Math.Clamp((comp.Delivery - comp.CoronaryFloor) / span, 0f, 1f);

        comp.Ceiling = ceiling * (comp.CoronaryMinimum + (1f - comp.CoronaryMinimum) * supply);

        var demandEv = new GetOxygenDemandEvent(1f);
        RaiseLocalEvent(ent, ref demandEv);
        var demand = demandEv.Demand;

        // Lying flat is the actual first aid for a hypovolemic collapse, and it is what lets a
        // fainted patient come round at all: on the floor they need less than they did upright.
        if (TryComp<StandingStateComponent>(ent, out var standing) && !standing.Standing)
            demand *= comp.LyingDemand;

        comp.Demand = Math.Max(0.1f, demand);

        // Stroke volume: an empty vessel has nothing to eject however fast it beats. Capped
        // at one because a transfused-full patient does not pump harder than a healthy one.
        // A damaged heart also squeezes weaker, which is asked for rather than read off the
        // heart directly — this system has never needed to know that organs exist.
        var strokeEv = new GetStrokeVolumeEvent(1f);
        RaiseLocalEvent(ent, ref strokeEv);

        var stroke = Math.Clamp(comp.Volume, 0f, 1f) * Math.Clamp(strokeEv.Multiplier, 0f, 1f);
        var perBeat = stroke * comp.Capacity * comp.Saturation;

        // The heart chases whatever rate would close the gap, and gets there slowly.
        var floor = Math.Min(comp.RestingRate, comp.Ceiling);

        var wanted = perBeat <= 0f
            ? comp.Ceiling
            : Math.Clamp(comp.Demand / perBeat * comp.RestingRate, floor, comp.Ceiling);

        comp.HeartRate = MoveToward(comp.HeartRate, wanted, comp.RateAdjust * dt);
        comp.Delivery = comp.HeartRate / comp.RestingRate * perBeat;

        var deficit = comp.Demand <= 0f
            ? 0f
            : Math.Clamp((comp.Demand - comp.Delivery) / comp.Demand, 0f, 1f);

        comp.Collapsed = comp.Volume < comp.CollapseVolume;

        SetStage(ent, deficit);
        ApplyDamage(ent, deficit, dt);
        TickFainting(ent, deficit, dt);
        ApplySymptoms(ent);

        Dirty(ent);
    }

    /// <summary>Reads the three multipliers off the body as it currently is.</summary>
    private void Measure(Entity<CirculationComponent> ent, BloodstreamComponent bloodstream)
    {
        var reference = bloodstream.BloodReferenceSolution.Volume;

        // Volume counts everything in the vessels — saline included, which is the entire
        // reason saline is worth carrying. Capacity counts only what carries oxygen.
        ent.Comp.Volume = reference <= 0
            || !_solutions.ResolveSolution(ent.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood)
                ? 0f
                : (blood.Volume / reference).Float();

        ent.Comp.Capacity = _bloodstream.GetBloodLevel(ent.Owner);
        ent.Comp.Saturation = GetSaturation(ent);
    }

    /// <summary>
    /// Saturation, taken from the respirator rather than modelled again.
    /// </summary>
    /// <remarks>
    /// Anyone who is breathing at all reads as fully saturated, and only somebody past the
    /// respirator's own suffocation threshold starts to slide. That strictness is deliberate:
    /// scaling smoothly across the whole breath buffer would leave the entire crew a few
    /// percent short of full delivery, which would raise everybody's resting pulse and hand
    /// healthy people heart disease for the crime of existing.
    /// <para>
    /// Floored rather than run to zero, because the respirator already deals its own
    /// asphyxiation damage and this would otherwise bill a suffocating patient twice. What
    /// the floor keeps is the part that matters: a hole in the hull makes blood loss worse.
    /// </para>
    /// </remarks>
    private float GetSaturation(Entity<CirculationComponent> ent)
    {
        var breathing = GetBreathingSaturation(ent);

        // Whatever the air is like, the lungs decide how much of it reaches the blood.
        var ceilingEv = new GetSaturationCeilingEvent(1f);
        RaiseLocalEvent(ent, ref ceilingEv);

        return Math.Clamp(Math.Min(breathing, ceilingEv.Ceiling), 0f, 1f);
    }

    /// <summary>Saturation as far as breathing alone is concerned.</summary>
    private float GetBreathingSaturation(Entity<CirculationComponent> ent)
    {
        if (!TryComp<RespiratorComponent>(ent, out var respirator)
            || respirator.Saturation >= respirator.SuffocationThreshold)
        {
            return 1f;
        }

        var span = respirator.SuffocationThreshold - respirator.MinSaturation;

        if (span <= 0f)
            return ent.Comp.SaturationFloor;

        var lost = Math.Clamp((respirator.SuffocationThreshold - respirator.Saturation) / span, 0f, 1f);

        return 1f - (1f - ent.Comp.SaturationFloor) * lost;
    }

    private void SetStage(Entity<CirculationComponent> ent, float deficit)
    {
        var stage = deficit > ent.Comp.ShockDeficit
            ? ShockStage.Shock
            : deficit > 0.01f
                ? ShockStage.Decompensating
                : ent.Comp.HeartRate > ent.Comp.CompensationRate
                    ? ShockStage.Compensating
                    : ShockStage.None;

        if (stage == ent.Comp.Stage)
            return;

        var old = ent.Comp.Stage;
        ent.Comp.Stage = stage;

        var ev = new ShockStageChangedEvent(old, stage);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Hypoxia: reversible, proportional to what is going unmet, and healed once it is not.
    /// </summary>
    /// <remarks>
    /// Scales with the deficit rather than with how much blood is missing, so a patient held
    /// together by a fast heart takes nothing at all. Being compensated is genuinely being
    /// fine — the price for it is paid by the heart, not by the damage counter.
    /// </remarks>
    private void ApplyDamage(Entity<CirculationComponent> ent, float deficit, float dt)
    {
        if (deficit > 0.01f)
        {
            _damageable.TryChangeDamage(
                ent.Owner,
                ent.Comp.HypoxiaDamage * deficit * dt,
                interruptsDoAfters: false);
            return;
        }

        // Only a heart that is not straining gets to pay off the debt.
        if (ent.Comp.Stage == ShockStage.None)
            _damageable.TryChangeDamage(ent.Owner, ent.Comp.HypoxiaRecovery * dt, ignoreResistances: true);
    }

    /// <summary>
    /// Passing out, as a spell you come round from rather than a switch that stays down.
    /// </summary>
    /// <remarks>
    /// Three rules, and each one exists because the obvious version was worse. It builds up
    /// over a couple of seconds instead of firing instantly, so half a bloodstream leaving in
    /// one go greys the world out and gives the player time to act on the warning. The spell
    /// is short. And after it there is a guaranteed window on your feet before the next one —
    /// without that window, anybody bled dry is unconscious until somebody else fixes them,
    /// which is not a punishment but a disconnection.
    /// <para>
    /// The status effect is applied once per spell with a fixed lifetime, never refreshed. A
    /// refreshed sleep is exactly the bug this replaces.
    /// </para>
    /// </remarks>
    private void TickFainting(Entity<CirculationComponent> ent, float deficit, float dt)
    {
        var comp = ent.Comp;
        var now = _timing.CurTime;

        // Still out cold. Nothing to decide until they come round.
        if (now < comp.FaintingUntil)
            return;

        // Just came round: sight back, and a few seconds of being visibly rattled rather than
        // snapping upright as though nothing happened.
        if (comp.FaintBlinded)
        {
            comp.FaintBlinded = false;
            RemComp<TemporaryBlindnessComponent>(ent);
            _status.TrySetStatusEffectDuration(ent.Owner, Woozy, comp.FaintGrogginess);
        }

        var failing = comp.Volume < comp.CollapseVolume || deficit > comp.FaintDeficit;

        if (!failing || now < comp.NextFaintAllowed)
        {
            comp.FaintPressure = Math.Max(0f, comp.FaintPressure - comp.FaintDecayRate * dt);

            if (comp.FaintPressure <= 0f)
                comp.FaintWarned = false;

            return;
        }

        comp.FaintPressure += comp.FaintBuildRate * dt;

        if (comp.FaintPressure < 1f)
        {
            // One warning per build-up, early enough to be worth something.
            if (comp.FaintPressure >= 0.4f && !comp.FaintWarned)
            {
                comp.FaintWarned = true;
                _popup.PopupEntity(Loc.GetString("is14-circulation-fainting-warning"), ent, ent, PopupType.MediumCaution);
            }

            return;
        }

        comp.FaintPressure = 0f;
        comp.FaintWarned = false;
        comp.FaintingUntil = now + comp.FaintDuration;
        comp.NextFaintAllowed = comp.FaintingUntil + comp.FaintRecovery;

        _status.TryAddStatusEffectDuration(ent.Owner, Collapse, comp.FaintDuration);

        // The screen going black is the whole difference between "I passed out" and "I tripped".
        // Knockdown alone reads as slipping on a banana peel.
        comp.FaintBlinded = true;
        EnsureComp<TemporaryBlindnessComponent>(ent);
        _popup.PopupEntity(Loc.GetString("is14-circulation-fainted"), ent, ent, PopupType.LargeCaution);
    }

    private void ApplySymptoms(Entity<CirculationComponent> ent)
    {
        switch (ent.Comp.Stage)
        {
            case ShockStage.Decompensating:
                _status.TrySetStatusEffectDuration(ent.Owner, Woozy, SymptomDuration);
                _status.TrySetStatusEffectDuration(ent.Owner, Stutter, SymptomDuration);
                break;

            case ShockStage.Shock:
                _status.TrySetStatusEffectDuration(ent.Owner, Bloodloss, SymptomDuration);
                _status.TrySetStatusEffectDuration(ent.Owner, Woozy, SymptomDuration);
                break;
        }
    }

    private static float MoveToward(float value, float target, float step)
    {
        if (Math.Abs(target - value) <= step)
            return target;

        return value + Math.Sign(target - value) * step;
    }
}

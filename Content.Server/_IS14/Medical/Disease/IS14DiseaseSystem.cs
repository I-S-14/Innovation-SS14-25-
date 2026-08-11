// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._IS14.Medical.Circulation;
using Content.Shared._IS14.Medical.Disease;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.Disease;

/// <summary>
/// The stage machine every IS14 condition runs on.
/// </summary>
/// <remarks>
/// Knows nothing about any particular illness. It moves progress, works out which rung that
/// lands on, keeps the current rung's symptoms attached, and answers the body's questions
/// about its own limits on the illnesses' behalf. What pushes an illness forward is somebody
/// else's job — see <see cref="Circulation.IschemiaSystem"/> for the first one.
/// <para>
/// Kept deliberately small and separate from Goobstation's virology, which is being developed
/// elsewhere and changes underneath us. Nothing here touches contagion, mutation or immunity;
/// if we ever want those, the right move is to ask that system, not to grow our own.
/// </para>
/// </remarks>
public sealed class IS14DiseaseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    /// <summary>Name our modifiers go under, so nothing else can trip over them.</summary>
    private const string DamageIdentifier = "IS14Disease";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SymptomLease = TimeSpan.FromSeconds(2.5);

    /// <summary>How long after the last push an illness counts as still being driven.</summary>
    private static readonly TimeSpan DriveGrace = TimeSpan.FromSeconds(2);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14DiseaseCarrierComponent, ComponentInit>(OnCarrierInit);
        SubscribeLocalEvent<IS14DiseaseComponent, ComponentShutdown>(OnDiseaseShutdown);
        SubscribeLocalEvent<IS14DiseaseCarrierComponent, GetHeartCeilingEvent>(OnGetCeiling);
        SubscribeLocalEvent<IS14DiseaseCarrierComponent, GetOxygenDemandEvent>(OnGetDemand);
    }

    private void OnCarrierInit(Entity<IS14DiseaseCarrierComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Diseases = _container.EnsureContainer<Container>(ent, IS14DiseaseCarrierComponent.ContainerId);
    }

    /// <summary>
    /// Lets go of the organ when the illness ends.
    /// </summary>
    /// <remarks>
    /// The surgery system keys its modifiers by the entity that applied them, so an illness
    /// that is simply deleted leaves its entry behind and the organ stays damaged for the rest
    /// of the round with nothing left to explain why. Curing has to hand the organ back.
    /// </remarks>
    private void OnDiseaseShutdown(Entity<IS14DiseaseComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Carrier is not { } carrier
            || ent.Comp.TargetOrgan is not { } slot
            || TerminatingOrDeleted(carrier)
            || FindOrgan(carrier, slot) is not { } organ)
        {
            return;
        }

        _trauma.TryRemoveOrganDamageModifier(organ, ent.Owner, DamageIdentifier);
    }

    // ── The body asking its illnesses what they have done to it ───────────────

    /// <summary>
    /// The lowest ceiling any active stage imposes wins.
    /// </summary>
    /// <remarks>
    /// Lowest rather than multiplied, because two separate reasons a heart cannot exceed 160
    /// do not combine into 120. The worst limit is simply the limit.
    /// </remarks>
    private void OnGetCeiling(Entity<IS14DiseaseCarrierComponent> ent, ref GetHeartCeilingEvent args)
    {
        foreach (var (_, stage) in ActiveStages(ent))
        {
            if (stage.HeartCeiling is { } ceiling)
                args.Ceiling = Math.Min(args.Ceiling, ceiling);
        }
    }

    private void OnGetDemand(Entity<IS14DiseaseCarrierComponent> ent, ref GetOxygenDemandEvent args)
    {
        foreach (var (_, stage) in ActiveStages(ent))
        {
            args.Demand *= stage.DemandMultiplier;
        }
    }

    // ── Ticking ───────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + Interval;
        var dt = (float) Interval.TotalSeconds;

        var query = EntityQueryEnumerator<IS14DiseaseCarrierComponent>();

        while (query.MoveNext(out var uid, out var carrier))
        {
            if (carrier.Diseases is not { } container)
                continue;

            // Copied because curing an illness deletes it out from under the iteration.
            var contained = new List<EntityUid>(container.ContainedEntities);

            foreach (var disease in contained)
            {
                if (!TryComp<IS14DiseaseComponent>(disease, out var comp))
                    continue;

                Tick((disease, comp), uid, dt);
            }

            UpdateChart((uid, carrier));
        }
    }

    /// <summary>
    /// Rebuilds the networked chart, and only tells the network when it actually changed.
    /// </summary>
    /// <remarks>
    /// Every organic humanoid carries this component and almost none of them are ill, so the
    /// common case has to cost nothing. Comparing before dirtying keeps a station full of
    /// healthy people off the wire entirely.
    /// </remarks>
    private void UpdateChart(Entity<IS14DiseaseCarrierComponent> ent)
    {
        var chart = new List<IS14Diagnosis>();

        foreach (var (disease, stage) in ActiveStages(ent))
        {
            chart.Add(new IS14Diagnosis(disease.Comp.Label, stage.Label, disease.Comp.Progress));
        }

        if (Enumerable.SequenceEqual(chart, ent.Comp.Diagnoses))
            return;

        ent.Comp.Diagnoses = chart;
        Dirty(ent);
    }

    private void Tick(Entity<IS14DiseaseComponent> disease, EntityUid carrier, float dt)
    {
        if (GetStage(disease) is not { } stage)
            return;

        // Only heals once whatever was causing it has let up. A rung with no regress rate
        // holds where it is regardless and needs treating.
        if (stage.RegressRate > 0f && _timing.CurTime - disease.Comp.LastDriven > DriveGrace)
            Advance(disease, -stage.RegressRate * dt);

        foreach (var effect in stage.Effects)
        {
            _status.TrySetStatusEffectDuration(carrier, effect, SymptomLease);
        }

        ApplyOrganDamage(disease, carrier, stage);
    }

    /// <summary>
    /// Holds the target organ's integrity down for as long as this stage lasts.
    /// </summary>
    /// <remarks>
    /// Written as one named modifier that gets overwritten, never accumulated, so the organ is
    /// exactly as damaged as the current stage says and no more. Curing the illness removes
    /// the entry and the organ is whole again, which is the behaviour a reversible diagnosis
    /// ought to have and a pile of applied damage never could.
    /// </remarks>
    private void ApplyOrganDamage(Entity<IS14DiseaseComponent> disease, EntityUid carrier, IS14DiseaseStage stage)
    {
        if (disease.Comp.TargetOrgan is not { } slot)
            return;

        if (FindOrgan(carrier, slot) is not { } organ)
            return;

        if (stage.OrganDamage <= 0)
        {
            _trauma.TryRemoveOrganDamageModifier(organ, disease.Owner, DamageIdentifier);
            return;
        }

        // Create-then-set, because the surgery API refuses to set a modifier it has never seen.
        _trauma.TryCreateOrganDamageModifier(organ, stage.OrganDamage, disease.Owner, DamageIdentifier);
        _trauma.TrySetOrganDamageModifier(organ, stage.OrganDamage, disease.Owner, DamageIdentifier);
    }

    private EntityUid? FindOrgan(EntityUid carrier, string slot)
    {
        foreach (var (uid, organ) in _body.GetBodyOrgans(carrier))
        {
            if (organ.SlotId == slot)
                return uid;
        }

        return null;
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gives somebody an illness, or finds the one they already have.
    /// </summary>
    /// <remarks>
    /// Idempotent on purpose: everything that drives an illness wants to say "this should be
    /// progressing" without first having to care whether it already exists.
    /// </remarks>
    public bool TryAddDisease(
        EntityUid target,
        EntProtoId proto,
        [NotNullWhen(true)] out Entity<IS14DiseaseComponent>? disease)
    {
        if (TryGetDisease(target, proto, out disease))
            return true;

        disease = null;

        if (!TryComp<IS14DiseaseCarrierComponent>(target, out var carrier) || carrier.Diseases is not { } container)
            return false;

        var uid = Spawn(proto);

        if (!TryComp<IS14DiseaseComponent>(uid, out var comp) || !_container.Insert(uid, container))
        {
            QueueDel(uid);
            return false;
        }

        comp.Carrier = target;
        disease = (uid, comp);
        UpdateStage(disease.Value);
        return true;
    }

    public bool TryGetDisease(
        EntityUid target,
        EntProtoId proto,
        [NotNullWhen(true)] out Entity<IS14DiseaseComponent>? disease)
    {
        disease = null;

        if (!TryComp<IS14DiseaseCarrierComponent>(target, out var carrier) || carrier.Diseases is not { } container)
            return false;

        foreach (var uid in container.ContainedEntities)
        {
            // An illness cured this tick is still in the container until deletion runs, and
            // handing it back would quietly resurrect it.
            if (TerminatingOrDeleted(uid)
                || MetaData(uid).EntityPrototype?.ID != proto.Id
                || !TryComp<IS14DiseaseComponent>(uid, out var comp))
            {
                continue;
            }

            disease = (uid, comp);
            return true;
        }

        return false;
    }

    /// <summary>Moves an illness along, and cures it if it falls off the bottom.</summary>
    public void Advance(Entity<IS14DiseaseComponent> disease, float delta)
    {
        if (delta > 0f)
            disease.Comp.LastDriven = _timing.CurTime;

        disease.Comp.Progress = Math.Clamp(disease.Comp.Progress + delta, 0f, 100f);

        if (disease.Comp.Progress <= 0f)
        {
            QueueDel(disease);
            return;
        }

        UpdateStage(disease);
        Dirty(disease);
    }

    /// <summary>The rung the current progress lands on, or null below the first one.</summary>
    public IS14DiseaseStage? GetStage(Entity<IS14DiseaseComponent> disease)
    {
        return disease.Comp.Stage <= 0 || disease.Comp.Stage > disease.Comp.Stages.Count
            ? null
            : disease.Comp.Stages[disease.Comp.Stage - 1];
    }

    private void UpdateStage(Entity<IS14DiseaseComponent> disease)
    {
        var stage = 0;

        for (var i = 0; i < disease.Comp.Stages.Count; i++)
        {
            if (disease.Comp.Progress >= disease.Comp.Stages[i].Threshold)
                stage = i + 1;
        }

        disease.Comp.Stage = stage;
    }

    /// <summary>Every stage currently in force across all of a carrier's illnesses.</summary>
    private IEnumerable<(Entity<IS14DiseaseComponent> Disease, IS14DiseaseStage Stage)> ActiveStages(
        Entity<IS14DiseaseCarrierComponent> ent)
    {
        if (ent.Comp.Diseases is not { } container)
            yield break;

        foreach (var uid in container.ContainedEntities)
        {
            if (!TryComp<IS14DiseaseComponent>(uid, out var comp))
                continue;

            Entity<IS14DiseaseComponent> disease = (uid, comp);

            if (GetStage(disease) is { } stage)
                yield return (disease, stage);
        }
    }
}

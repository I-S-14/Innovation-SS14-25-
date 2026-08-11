// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._IS14.Medical.Disease;
using Content.Shared._IS14.Medical.Circulation;
using Content.Shared._IS14.Medical.Disease;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.Circulation;

/// <summary>
/// What pushes ischaemic heart disease forward: the compensation itself.
/// </summary>
/// <remarks>
/// The loop this closes is the reason the whole model was worth building. Blood loss makes the
/// heart race; a racing heart wears out; a worn-out heart cannot race; a heart that cannot
/// race cannot compensate — so the patient who was stable for ten minutes collapses, and the
/// reason was on the analyser the whole time.
/// <para>
/// Strain is read off the circulation and nothing else. This system never touches damage,
/// symptoms or stages; it only says how hard the heart is working, and the disease system
/// turns that into a diagnosis.
/// </para>
/// </remarks>
public sealed class IschemiaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IS14DiseaseSystem _disease = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly EntProtoId Ischemia = "IS14DiseaseIschemia";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Progress per second at one unit of strain.
    /// </summary>
    /// <remarks>
    /// Tuned so that a couple of minutes of genuine shock lands the patient in angina, while a
    /// long stretch of quiet tachycardia gets them no further than an oxygen debt they will
    /// sleep off. Those two outcomes are the pacing; the number exists to produce them.
    /// </remarks>
    private const float ProgressRate = 0.11f;

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + Interval;
        var dt = (float) Interval.TotalSeconds;

        var query = EntityQueryEnumerator<CirculationComponent, IS14DiseaseCarrierComponent>();

        while (query.MoveNext(out var uid, out var circulation, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            var strain = GetStrain(circulation);

            if (strain <= 0f)
                continue;

            // The first strained second is what gives somebody the diagnosis at all.
            if (_disease.TryAddDisease(uid, Ischemia, out var disease))
                _disease.Advance(disease.Value, strain * ProgressRate * dt);
        }
    }

    /// <summary>
    /// How hard this heart is working beyond what it should have to.
    /// </summary>
    /// <remarks>
    /// Two sources, deliberately unequal. Tachycardia alone is wear and tear and tops out at
    /// one; going without oxygen is several times worse and is what actually ruins a heart. A
    /// patient held together by a fast pulse is being damaged slowly; a patient in shock is
    /// being damaged quickly.
    /// </remarks>
    private static float GetStrain(CirculationComponent comp)
    {
        var strain = 0f;
        var headroom = comp.MaxRate - comp.CompensationRate;

        if (headroom > 0f && comp.HeartRate > comp.CompensationRate)
        {
            // Squared, so the wear is not linear in the rate. A hundred and twenty for a while
            // is a tired heart; a hundred and ninety for the same while is a damaged one, and a
            // straight line between the two would have flattered the second badly.
            var over = Math.Clamp((comp.HeartRate - comp.CompensationRate) / headroom, 0f, 1f);
            strain += over * over * 2f;
        }

        strain += comp.Stage switch
        {
            ShockStage.Decompensating => 1f,
            ShockStage.Shock => 3f,
            _ => 0f,
        };

        return strain;
    }
}

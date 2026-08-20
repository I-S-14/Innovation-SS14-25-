// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.Circulation;
using Content.Shared._IS14.Medical.Organs;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.Organs;

/// <summary>
/// The half of the loop that runs the other way: poor circulation wears out the organs.
/// </summary>
/// <remarks>
/// Without this the medical model has no memory. A patient bled to the floor and refilled is
/// exactly the patient they were before, so there is no difference between reaching them in
/// one minute and in ten, and therefore nothing a good doctor can be good at. Here the
/// difference is written into the body: the fast rescue walks away, the slow one walks away
/// with a heart that no longer covers a sprint and kidneys that will need watching.
/// <para>
/// See <c>Docs/_IS14/organ-function-design.md</c> §5.
/// </para>
/// </remarks>
public sealed class OrganPerfusionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly OrganFunctionSystem _function = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Function the most sensitive organ loses per second at a total shortfall.
    /// </summary>
    /// <remarks>
    /// Calibrated against the case that matters, which is not arrest but the long middle: a
    /// patient left in decompensated shock — a third of demand unmet — for two minutes comes
    /// out of it having lost about a fifth of their brain function. Slow enough that a doctor
    /// who arrives has time to prevent it, fast enough that one who does not arrive is the
    /// reason it happened.
    /// </remarks>
    private const float InjuryRate = 0.006f;

    /// <summary>Share of that rate at which the damage undoes itself once oxygen is back.</summary>
    /// <remarks>
    /// A tenth, which puts full recovery in the tens of minutes. Long enough that surviving
    /// badly is a state a doctor can find, treat and be thanked for; short enough that nobody
    /// spends the rest of the round as the person who bled out once.
    /// </remarks>
    private const float RecoveryFraction = 0.1f;

    /// <summary>Shortfall below which the body counts as coping.</summary>
    private const float Threshold = 0.01f;

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + Interval;
        var dt = (float) Interval.TotalSeconds;

        var query = EntityQueryEnumerator<CirculationComponent, IS14FacultiesComponent>();

        while (query.MoveNext(out var uid, out var circulation, out var faculties))
        {
            // The dead are not modelled. Circulation stops stepping them, so their metrics are
            // frozen at whatever killed them and would grind their organs down forever — and
            // deciding what a corpse owes on revival is a question for the defibrillator, not
            // for a loop that cannot see it coming.
            if (_mobState.IsDead(uid))
                continue;

            Step(uid, circulation, faculties, dt);
        }
    }

    private void Step(EntityUid body, CirculationComponent circulation, IS14FacultiesComponent faculties, float dt)
    {
        var deficit = circulation.Demand <= 0f
            ? 0f
            : Math.Clamp((circulation.Demand - circulation.Delivery) / circulation.Demand, 0f, 1f);

        var starving = deficit > Threshold;

        // A body that is coping and owes nothing is the overwhelming majority of the station,
        // and it costs one comparison rather than a walk through seven organs.
        if (!starving && !faculties.HypoxicDebt)
            return;

        var changed = false;
        var debt = false;

        foreach (var (uid, organ) in _body.GetBodyOrgans(body))
        {
            if (!TryComp<IS14OrganFunctionComponent>(uid, out var function)
                || function.PerfusionSensitivity <= 0f)
            {
                continue;
            }

            var before = function.HypoxicInjury;

            var after = starving
                ? Math.Min(
                    function.InjuryCap,
                    before + deficit * InjuryRate * function.PerfusionSensitivity * dt)
                : Math.Max(0f, before - InjuryRate * RecoveryFraction * dt);

            debt |= after > 0f;

            if (Math.Abs(after - before) < 0.0001f)
                continue;

            function.HypoxicInjury = after;
            Dirty(uid, function);
            changed = true;
        }

        faculties.HypoxicDebt = debt;

        // One recompute for the whole body rather than one per organ, since every organ that
        // moved this tick moved for the same reason and the levels are read as a set.
        if (changed)
            _function.Recompute(body);
    }
}

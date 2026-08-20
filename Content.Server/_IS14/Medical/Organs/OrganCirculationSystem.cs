// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.Circulation;
using Content.Shared._IS14.Medical.Organs;
using Content.Shared.Body.Systems;

namespace Content.Server._IS14.Medical.Organs;

/// <summary>
/// Where the heart and the lungs meet the circulation model.
/// </summary>
/// <remarks>
/// Three subscriptions and no state. The circulation system asks three questions about its own
/// limits every second and this answers two and a half of them from organ function, which is
/// the whole of phase two: the largest behavioural change in the feature costs almost no code
/// because both halves were built to be asked rather than to know.
/// <para>
/// See <c>Docs/_IS14/organ-function-design.md</c> §4.1–4.2.
/// </para>
/// </remarks>
public sealed class OrganCirculationSystem : EntitySystem
{
    [Dependency] private readonly SharedInternalsSystem _internals = default!;
    [Dependency] private readonly SharedOrganFunctionSystem _function = default!;

    /// <summary>
    /// Share of the lungs' lost capacity that breathing rich gas gives back.
    /// </summary>
    /// <remarks>
    /// Half, and never all of it — a mask raises the pressure of what reaches the alveoli, it
    /// does not grow new ones. What this buys is a reason to put a mask on a patient with a
    /// hole in their chest, and the matching cruelty that the same patient in vacuum without
    /// one is worse off than a healthy person would be.
    /// </remarks>
    private const float OxygenBypass = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14FacultiesComponent, GetHeartCeilingEvent>(OnGetCeiling);
        SubscribeLocalEvent<IS14FacultiesComponent, GetStrokeVolumeEvent>(OnGetStroke);
        SubscribeLocalEvent<IS14FacultiesComponent, GetSaturationCeilingEvent>(OnGetSaturationCeiling);
    }

    /// <summary>
    /// A weak heart cannot race.
    /// </summary>
    /// <remarks>
    /// Multiplied rather than floored, because unlike a diagnosis — which says "this heart may
    /// not exceed 160" — damage says "this heart does less of everything". The two stack in
    /// the obvious way and the lower of them wins on its own.
    /// </remarks>
    private void OnGetCeiling(Entity<IS14FacultiesComponent> ent, ref GetHeartCeilingEvent args)
    {
        args.Ceiling *= Pump(ent);
    }

    /// <summary>
    /// A weak heart also squeezes weaker, which is the half that bites immediately.
    /// </summary>
    /// <remarks>
    /// Applying function to the rate and to the volume means peak delivery falls with the
    /// square of the damage. That is deliberate and it is why heart injuries are frightening:
    /// a heart at two-thirds still covers a resting body comfortably, and a heart at a third
    /// cannot cover one at all.
    /// </remarks>
    private void OnGetStroke(Entity<IS14FacultiesComponent> ent, ref GetStrokeVolumeEvent args)
    {
        args.Multiplier *= Pump(ent);
    }

    private void OnGetSaturationCeiling(Entity<IS14FacultiesComponent> ent, ref GetSaturationCeilingEvent args)
    {
        var respiration = _function.GetLevel(ent, IS14Faculties.Respiration);

        if (respiration >= 1f)
            return;

        if (_internals.AreInternalsWorking(ent.Owner))
            respiration += (1f - respiration) * OxygenBypass;

        args.Ceiling = Math.Min(args.Ceiling, respiration);
    }

    /// <summary>
    /// Cardiac function, floored well above zero.
    /// </summary>
    /// <remarks>
    /// A heart that has stopped is somebody else's ruling — a diagnosis, a defibrillator, or
    /// death. Organ damage alone is not allowed to be the thing that ends the beat, or a bad
    /// enough chest wound would kill silently with nothing on the readout to argue with.
    /// </remarks>
    private float Pump(EntityUid body) => Math.Max(0.1f, _function.GetLevel(body, IS14Faculties.Pump));
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Medical.Circulation;

/// <summary>
/// How well this body is actually delivering oxygen, and what it is doing to keep that up.
/// </summary>
/// <remarks>
/// Replaces the upstream "blood level below a threshold, start dealing damage" rule with the
/// real product: delivery is cardiac output times carrying capacity times saturation. Three
/// multipliers, broken by three different disasters, collapsing into one number that decides
/// everything — which is why bleeding, a bag of saline, the wrong blood group and a hole in
/// the hull need no separate code between them.
/// <para>
/// See <c>Docs/_IS14/bloodloss-design.md</c>.
/// </para>
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CirculationComponent : Component
{
    // ── Live metrics ──────────────────────────────────────────────────────────
    // Networked because the alert, the pulse verb and the analyser all read them, and
    // because a doctor watching a patient should not be watching a value that lags a second
    // behind what the server thinks.

    /// <summary>Beats per minute right now.</summary>
    [ViewVariables, AutoNetworkedField]
    public float HeartRate;

    /// <summary>
    /// Fraction of the carried blood that is actually loaded with oxygen, 0..1.
    /// </summary>
    /// <remarks>
    /// Derived from the upstream respirator rather than modelled separately: the game already
    /// decides whether you are breathing, and a second opinion about it would only ever
    /// disagree with the first.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public float Saturation = 1f;

    /// <summary>Fluid in the vessels, as a fraction of normal. Drives stroke volume.</summary>
    [ViewVariables, AutoNetworkedField]
    public float Volume = 1f;

    /// <summary>Oxygen-carrying blood, as a fraction of normal. Always at most <see cref="Volume"/>.</summary>
    [ViewVariables, AutoNetworkedField]
    public float Capacity = 1f;

    /// <summary>Oxygen actually reaching tissue, as a fraction of the resting requirement.</summary>
    [ViewVariables, AutoNetworkedField]
    public float Delivery = 1f;

    /// <summary>What the body is asking for. Rises with exertion, falls with cold and rest.</summary>
    [ViewVariables, AutoNetworkedField]
    public float Demand = 1f;

    /// <summary>Highest rate this heart can currently reach. Lowered by heart disease.</summary>
    [ViewVariables, AutoNetworkedField]
    public float Ceiling;

    /// <summary>Where the patient currently is on the shock ladder.</summary>
    [ViewVariables, AutoNetworkedField]
    public ShockStage Stage;

    /// <summary>Whether volume has fallen far enough that there is nothing left to pump.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Collapsed;

    /// <summary>
    /// How close this body is to passing out, 0 to 1.
    /// </summary>
    /// <remarks>
    /// A build-up rather than a threshold, so that losing half your blood in one go greys the
    /// world out over a couple of seconds instead of switching you off between two frames.
    /// Those seconds are not decoration: they are long enough to hit an injector, shout, or
    /// lie down on purpose.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public float FaintPressure;

    /// <summary>When the current fainting spell ends.</summary>
    [ViewVariables]
    public TimeSpan FaintingUntil;

    /// <summary>Earliest the body may faint again.</summary>
    [ViewVariables]
    public TimeSpan NextFaintAllowed;

    /// <summary>Whether a warning has already been given for the current build-up.</summary>
    [ViewVariables]
    public bool FaintWarned;

    // ── Tuning ────────────────────────────────────────────────────────────────

    /// <summary>Beats per minute with nothing wrong.</summary>
    [DataField]
    public float RestingRate = 70f;

    /// <summary>Beats per minute a healthy heart can reach at full compensation.</summary>
    [DataField]
    public float MaxRate = 200f;

    /// <summary>
    /// How fast the rate chases the rate it wants, in beats per minute per second.
    /// </summary>
    /// <remarks>
    /// The lag is the point. A transfusion fixes capacity in seconds, but the heart takes
    /// the better part of a minute to come back down — so the patient stays visibly unwell
    /// after the bag has done its work, and the doctor has a reason to stand there.
    /// </remarks>
    [DataField]
    public float RateAdjust = 6f;

    /// <summary>Rate above which the body counts as compensating rather than resting.</summary>
    [DataField]
    public float CompensationRate = 100f;

    /// <summary>
    /// Delivery at or above which the heart is fed well enough to reach its full rate.
    /// </summary>
    /// <remarks>
    /// The heart is supplied by the same circulation it drives, so a failing one cannot race.
    /// Below this the achievable rate slides down with the supply, and a patient who has lost
    /// most of their blood goes <em>slow</em> rather than fast — terminal bradycardia, which is
    /// what dying of blood loss actually looks like and is a far worse sign than tachycardia.
    /// <para>
    /// Read from last tick's delivery, which both avoids the circularity and gives the spiral
    /// its inertia: less supply, lower rate, less supply again.
    /// </para>
    /// </remarks>
    [DataField]
    public float CoronaryComfort = 0.45f;

    /// <summary>Delivery below which the heart is barely supplied at all.</summary>
    [DataField]
    public float CoronaryFloor = 0.05f;

    /// <summary>Share of its ceiling a wholly unsupplied heart can still manage.</summary>
    /// <remarks>
    /// Not zero: a starving heart goes agonal rather than stopping outright, and stopping is
    /// the job of a diagnosis rather than of a threshold.
    /// </remarks>
    [DataField]
    public float CoronaryMinimum = 0.15f;

    /// <summary>Share of demand that may go unmet before shock proper begins.</summary>
    [DataField]
    public float ShockDeficit = 0.3f;

    /// <summary>Volume below which there is not enough fluid to stay upright at all.</summary>
    [DataField]
    public float CollapseVolume = 0.6f;

    /// <summary>Deficit that will drop somebody even when they have fluid enough to pump.</summary>
    [DataField]
    public float FaintDeficit = 0.5f;

    /// <summary>How long one fainting spell lasts.</summary>
    /// <remarks>
    /// Short on purpose. A faint is meant to be a punishing interruption, not a removal from
    /// the round — the patient comes round, sees the state they are in, and gets a window to
    /// do something about it.
    /// </remarks>
    [DataField]
    public TimeSpan FaintDuration = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Guaranteed time on your feet after coming round before you can faint again.
    /// </summary>
    /// <remarks>
    /// The single most important number here. Without it, somebody bled dry is unconscious
    /// forever with no way to act, which is not a punishment but a disconnection. With it,
    /// hypovolemia becomes what it really is: repeated collapse with lucid gaps in between,
    /// and the gaps are where the player crawls, screams, or injects something.
    /// </remarks>
    [DataField]
    public TimeSpan FaintRecovery = TimeSpan.FromSeconds(12);

    /// <summary>How fast the world greys out while the body is failing, per second.</summary>
    [DataField]
    public float FaintBuildRate = 0.34f;

    /// <summary>How fast it clears once the body is coping again, per second.</summary>
    [DataField]
    public float FaintDecayRate = 0.5f;

    /// <summary>
    /// Multiplier on oxygen demand while lying down.
    /// </summary>
    /// <remarks>
    /// Lying flat is genuinely the first aid for hypovolemic collapse, so it is worth
    /// something here too: a patient on the floor needs less than a patient on their feet, and
    /// that is what lets them come round at all. It also means "stay down" is real advice
    /// rather than flavour.
    /// </remarks>
    [DataField]
    public float LyingDemand = 0.75f;

    /// <summary>
    /// How far saturation is allowed to drag delivery down.
    /// </summary>
    /// <remarks>
    /// Floored deliberately. Suffocation is already punished by the respirator, and letting
    /// saturation reach zero here would bill a suffocating patient twice for the same breath.
    /// The floor keeps a hole in the hull relevant to delivery without making this system the
    /// main way to die of vacuum.
    /// </remarks>
    [DataField]
    public float SaturationFloor = 0.5f;

    /// <summary>Damage per second when delivery has completely failed, scaled by the deficit.</summary>
    /// <remarks>
    /// Dealt as <c>Bloodloss</c> and not as <c>Asphyxiation</c>, which was the obvious choice
    /// and the wrong one: the respirator heals a flat point of asphyxiation every breath for
    /// anybody who is breathing, so hypoxia from blood loss was being wiped out as fast as it
    /// was dealt. <c>Bloodloss</c> has no such carer now that the upstream branch is gated off
    /// — this system is the only thing that writes it, and the only thing that clears it.
    /// </remarks>
    [DataField]
    public DamageSpecifier HypoxiaDamage = new()
    {
        DamageDict = { ["Bloodloss"] = 3.0f },
    };

    /// <summary>Damage healed per second while delivery is meeting demand.</summary>
    [DataField]
    public DamageSpecifier HypoxiaRecovery = new()
    {
        DamageDict = { ["Bloodloss"] = -0.8f },
    };

    /// <summary>When this body was last stepped.</summary>
    [ViewVariables]
    public TimeSpan NextUpdate;
}

/// <summary>
/// The shock ladder. Determined by whether demand is being met and how hard the heart is
/// working to meet it — not by how much blood is left, which is only one of three ways to
/// end up here.
/// </summary>
public enum ShockStage : byte
{
    /// <summary>Demand met, heart at rest.</summary>
    None,

    /// <summary>Demand met, but only because the heart is racing to meet it.</summary>
    Compensating,

    /// <summary>Compensation has run out and some of the demand is going unmet.</summary>
    Decompensating,

    /// <summary>A large share of demand is unmet. Organs are being damaged.</summary>
    Shock,
}

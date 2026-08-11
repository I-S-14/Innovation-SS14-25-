// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Medical.Disease;

/// <summary>
/// An illness, living as its own entity inside the patient who has it.
/// </summary>
/// <remarks>
/// An entity rather than a number on the mob, because a diagnosis is a thing you can name,
/// tell somebody over the radio and treat on purpose — "ischaemia, second stage" is all three,
/// and "40 cellular damage" is none of them.
/// <para>
/// Deliberately separate from Goobstation's disease system, which is aimed at virology,
/// contagion and mutation and is under active development elsewhere. Ours is a plain stage
/// machine for conditions the body inflicts on itself, and it is kept apart so that neither
/// can break the other. Hence the <c>IS14</c> prefix on every name here: the two live side by
/// side and must never share a component id.
/// </para>
/// <para>
/// See <c>Docs/_IS14/bloodloss-design.md</c>.
/// </para>
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IS14DiseaseComponent : Component
{
    /// <summary>What the analyser calls it.</summary>
    [DataField(required: true)]
    public LocId Label;

    /// <summary>
    /// Organ slot this illness attacks, if it attacks one. Null means it harms no organ.
    /// </summary>
    /// <remarks>
    /// Named by slot — "heart", "lungs" — rather than by a component, so a new illness of some
    /// other organ is a line of YAML. The damage itself goes through the surgery system's
    /// integrity modifiers, which is the only thing in the game that actually owns organ
    /// health; writing a damage type at the body instead would be inventing a second answer to
    /// a question already answered.
    /// </remarks>
    [DataField]
    public string? TargetOrgan;

    /// <summary>
    /// The ladder, shallowest first. Stage 1 is the first entry.
    /// </summary>
    [DataField(required: true)]
    public List<IS14DiseaseStage> Stages = new();

    /// <summary>How far along the illness is, 0 to 100.</summary>
    [DataField, AutoNetworkedField]
    public float Progress;

    /// <summary>Which rung of <see cref="Stages"/> is active, counting from 1. Zero is none.</summary>
    [DataField, AutoNetworkedField]
    public int Stage;

    /// <summary>Who has it.</summary>
    [ViewVariables]
    public EntityUid? Carrier;

    /// <summary>When something last pushed this illness forward.</summary>
    /// <remarks>
    /// Regress is held off while a driver is active, rather than being subtracted from the
    /// drive every tick. Summing them read fine on paper and was almost exactly wrong in
    /// practice: a stage that heals at 0.2 a second against a driver pushing 0.22 crawls
    /// forward at 0.02, so a patient with half their blood gone developed nothing at all.
    /// An illness gets better when its cause stops, not while it is happening.
    /// </remarks>
    [ViewVariables]
    public TimeSpan LastDriven;
}

/// <summary>
/// One rung of an illness: what it is called, when it starts, and what it does while it lasts.
/// </summary>
[DataDefinition]
public sealed partial class IS14DiseaseStage
{
    /// <summary>Progress at which this stage takes over.</summary>
    [DataField(required: true)]
    public float Threshold;

    /// <summary>What the analyser calls this rung.</summary>
    [DataField(required: true)]
    public LocId Label;

    /// <summary>
    /// Status effects kept on the patient for as long as this stage is the active one.
    /// </summary>
    /// <remarks>
    /// Reuses the engine's status effect machinery rather than inventing a second way to hang
    /// a symptom on somebody — that part of the game is stable and shared by everything.
    /// Refreshed on a short lease, so symptoms fall off by themselves the moment the stage
    /// changes or the illness is cured.
    /// </remarks>
    [DataField]
    public List<EntProtoId> Effects = new();

    /// <summary>
    /// Ceiling this stage imposes on the heart, in beats per minute. Null leaves it alone.
    /// </summary>
    /// <remarks>
    /// The sharpest tool in the box, and it does no damage at all. It removes the headroom
    /// that was keeping a bleeding patient upright, and the wound they already had finishes
    /// the job. Zero is cardiac arrest.
    /// </remarks>
    [DataField]
    public float? HeartCeiling;

    /// <summary>Multiplier this stage applies to how much oxygen the body is asking for.</summary>
    [DataField]
    public float DemandMultiplier = 1f;

    /// <summary>
    /// How much of the target organ's integrity this stage holds down, while it lasts.
    /// </summary>
    /// <remarks>
    /// A standing modifier, not a tick of damage. That is the difference between "this heart is
    /// currently damaged because of the infarction" and "the infarction hit it forty times" —
    /// the first can be undone by curing the illness, and it is also how the rest of the game
    /// already models a hurt organ.
    /// <para>
    /// Most rungs should leave this at zero. It is for the ones where tissue is genuinely
    /// dying, which is a different claim from "the patient feels unwell".
    /// </para>
    /// </remarks>
    [DataField]
    public FixedPoint2 OrganDamage;

    /// <summary>
    /// Progress shed per second while nothing is driving this illness forward. Zero means this
    /// rung does not clear on its own and something has to treat it.
    /// </summary>
    /// <remarks>
    /// Per stage rather than per illness on purpose: the shallow rungs are a debt the body can
    /// pay off by resting, and the deep ones are a diagnosis somebody has to deal with. That
    /// difference is the whole reason a slow rescue costs more than a fast one.
    /// </remarks>
    [DataField]
    public float RegressRate;
}

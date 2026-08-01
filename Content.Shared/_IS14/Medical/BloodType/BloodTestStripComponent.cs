// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// A paper typing card: bleed on it, wait, read it yourself.
/// </summary>
/// <remarks>
/// The cheap half of the pair. Where the analyser costs four seconds and hands you a group,
/// the strip costs a syringe of blood and hands you three wells — the doctor still has to
/// know that A-positive and D-positive means A+. That is the entire difference, and it is
/// why the strip can be stocked by the boxful while the analyser stays a single device on
/// a shelf: one of them makes you competent, the other one makes you equipped.
/// </remarks>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BloodTestStripComponent : Component
{
    /// <summary>The pad the sample soaks into.</summary>
    [DataField]
    public string Solution = "strip";

    /// <summary>
    /// Which blood this card is printed for. Decides which wells it has before anybody
    /// bleeds on it, and what it refuses to read afterwards.
    /// </summary>
    /// <remarks>
    /// A card is manufactured with particular antibodies dried into it, so a human card has
    /// three wells whether or not it has been used, and a moth bleeding on one gets nothing.
    /// Working it out from the sample instead would mean a blank card had no wells to print.
    /// </remarks>
    [DataField]
    public ProtoId<ReagentPrototype> Card = "Blood";

    /// <summary>How much blood it takes to wet the pad enough to read.</summary>
    [DataField]
    public FixedPoint2 Required = FixedPoint2.New(1);

    /// <summary>
    /// How long the reaction takes to come up and be readable.
    /// </summary>
    /// <remarks>
    /// Long enough that you cannot stand over it — the doctor is meant to stick it to the
    /// patient's chart and go do something else, then come back to an answer.
    /// </remarks>
    [DataField]
    public TimeSpan DevelopDelay = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Whether a sample has been taken. Set once and never cleared: the card reads the first
    /// blood to touch it, so topping it up afterwards cannot walk the answer back.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Used;

    /// <summary>Which blood the sample was, deciding which wells the card even has.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>
    /// Which wells came up. Stored as the antigens themselves rather than as a group name,
    /// because a card that soaked up a mixture may have no group to name — and a card that
    /// stored a null name would redraw itself as three negatives, which is a card confidently
    /// claiming O-negative.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<BloodAntigenPrototype>> Antigens = new();

    /// <summary>When the reaction finishes. Before this the card just says it is working.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan DevelopAt;

    /// <summary>
    /// Whose blood this is supposed to be, written on by hand.
    /// </summary>
    /// <remarks>
    /// The card has no idea and never will — this is whatever the doctor wrote, which is the
    /// only thing tying a developed strip to a patient once it leaves their hand. It can be
    /// wrong, and a ward full of strips somebody labelled carelessly is a real way to kill
    /// somebody.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public string Patient = string.Empty;

    /// <summary>
    /// Whether the field is currently open for writing. Set by clicking the card with a pen,
    /// cleared when the window closes — the same bargain a sheet of paper makes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Writable;
}

/// <summary>Sent when somebody types a name onto the card.</summary>
[Serializable, NetSerializable]
public sealed class BloodStripSetPatientMessage : BoundUserInterfaceMessage
{
    public readonly string Patient;

    public BloodStripSetPatientMessage(string patient)
    {
        Patient = patient;
    }
}

/// <summary>
/// The stain, which appears the instant blood lands — long before the reaction is readable.
/// A spent card is meant to be obvious in a pile of fresh ones.
/// </summary>
[Serializable, NetSerializable]
public enum BloodTestStripVisuals : byte
{
    Used,
}

[Serializable, NetSerializable]
public enum BloodTestStripLayers : byte
{
    Reaction,
}

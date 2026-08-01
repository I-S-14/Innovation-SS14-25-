// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// A blood group: which reagent it is made of, and which antigens it carries.
/// </summary>
/// <remarks>
/// The set of groups a species can roll is not written down anywhere — it is every
/// prototype that names that species' blood <see cref="Reagent"/> and has a non-zero
/// <see cref="Weight"/>. Adding a group to a species is therefore one prototype and no
/// code, and a species nobody has written groups for keeps behaving exactly as it did
/// before this system existed.
/// </remarks>
[Prototype("bloodType")]
public sealed partial class BloodTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Spelled out, e.g. "вторая отрицательная".</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>How it is written on a label, e.g. "A−".</summary>
    [DataField(required: true)]
    public LocId ShortName;

    /// <summary>
    /// Which blood this is a group of. Two types built on different reagents are never
    /// compatible, which is how cross-species transfusion is rejected without a single
    /// line of species-aware code.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Blood";

    /// <summary>
    /// What this blood is marked with. Empty means a universal donor: there is nothing on
    /// it for a recipient to object to.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<BloodAntigenPrototype>> Antigens = new();

    /// <summary>
    /// Relative frequency in the population. Zero keeps the group out of the natural roll
    /// while leaving it valid to hand out deliberately — synthetic blood, admin spawns,
    /// and whatever xenobiology grows next.
    /// </summary>
    [DataField]
    public float Weight;

    /// <summary>Tint for labels and test readouts. Null falls back to the reagent's colour.</summary>
    [DataField]
    public Color? Color;

    /// <summary>
    /// What this blood becomes when a recipient rejects it. Null uses the default hemolysate.
    /// </summary>
    /// <remarks>
    /// Here so that strange blood can fail strangely — something that clots, burns or crawls
    /// instead of merely poisoning — without the transfusion code learning about it.
    /// </remarks>
    [DataField]
    public ProtoId<ReagentPrototype>? RejectedReagent;
}

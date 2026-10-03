// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A station-wide number that technologies can move: print speed, payout rates, and so on.
/// </summary>
/// <remarks>
/// This is the base that lets a technology do something other than unlock a recipe. A new
/// knob is a prototype plus one place that reads it — no changes to the research code.
/// </remarks>
[Prototype("is14ResearchModifier")]
public sealed partial class IS14ResearchModifierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Player-facing name, used when a card explains what it changes.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Value with nothing researched.</summary>
    [DataField]
    public float Default = 1f;

    /// <summary>Clamps the accumulated value, so a stack of upgrades cannot break a system.</summary>
    [DataField]
    public float Minimum = float.MinValue;

    [DataField]
    public float Maximum = float.MaxValue;

    /// <summary>
    /// When set, this modifier only applies to payouts of that research data type.
    /// Lets one consumer serve "all data" and "biological data only" alike.
    /// </summary>
    [DataField]
    public ProtoId<ResearchPointTypePrototype>? PointType;

    /// <summary>
    /// Lathes this modifier applies to, children included. Empty means every lathe on the
    /// station — which is what makes "наладка медицинского цеха" a different decision from
    /// "наладка всего производства".
    /// </summary>
    [DataField]
    public List<EntProtoId> Lathes = new();

    /// <summary>Which lathe number this modifier moves, when it is scoped to lathes.</summary>
    [DataField]
    public IS14LatheKnob? Knob;

    /// <summary>True when a higher number is an improvement. Display only.</summary>
    [DataField]
    public bool HigherIsBetter = true;
}

/// <summary>What a lathe-scoped modifier multiplies.</summary>
public enum IS14LatheKnob : byte
{
    /// <summary>Print time. Lower is faster.</summary>
    Speed,

    /// <summary>Material use. Lower is cheaper.</summary>
    Material,
}

/// <summary>
/// Accumulated modifiers of a station. Lives next to the research balances.
/// </summary>
[RegisterComponent]
public sealed partial class IS14ResearchModifiersComponent : Component
{
    /// <summary>Sum of everything researched, before the prototype's default is added.</summary>
    [DataField]
    public Dictionary<ProtoId<IS14ResearchModifierPrototype>, float> Deltas = new();
}

/// <summary>Raised on the station when a modifier moves, so consumers can retune.</summary>
[ByRefEvent]
public readonly record struct IS14ResearchModifiersChangedEvent(EntityUid Station);

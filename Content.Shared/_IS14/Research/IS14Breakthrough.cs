// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A technology that cannot be bought with data at all: it is paid for with a physical object,
/// destroyed for good in the destructive analyzer.
/// </summary>
/// <remarks>
/// This is the only mechanism in the department that makes one specific object on the map
/// matter. No crystal, no bluespace — however much scientific data has piled up. The sample is
/// named here rather than hidden in code so a player can read the console, see exactly what is
/// wanted, and go get it: deterministic by design, because a breakthrough you cannot plan for
/// is just a wall.
/// </remarks>
[DataDefinition]
public sealed partial class IS14BreakthroughRequirement
{
    /// <summary>What to bring, in words: "блюспейс-кристалл".</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Prototypes that satisfy it, parents included — <c>BaseAnomalyCore</c> accepts any core.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Samples = new();

    /// <summary>Prototype whose sprite the console draws. Defaults to the first sample.</summary>
    [DataField]
    public EntProtoId? Icon;

    /// <summary>Where it is usually found, for the console card.</summary>
    [DataField]
    public LocId? Hint;

    /// <summary>Research data the teardown itself pays, on top of opening the way.</summary>
    [DataField]
    public ProtoId<ResearchPointTypePrototype> PointType = "Science";

    [DataField]
    public int Payout = 40;
}

/// <summary>
/// Breakthroughs this station has paid for. One entry per technology, and it never goes away:
/// the sample is gone, the way is open for the rest of the shift.
/// </summary>
[RegisterComponent]
public sealed partial class IS14BreakthroughComponent : Component
{
    /// <summary>Technology IDs whose sample has been analysed.</summary>
    [DataField]
    public HashSet<string> Completed = new();
}

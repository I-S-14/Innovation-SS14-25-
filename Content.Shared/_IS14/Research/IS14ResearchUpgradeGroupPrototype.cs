// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// One slot on the research map that holds several mutually exclusive upgrades. Exactly one
/// variant is revealed per round, so the same place in the tree offers a different trade-off
/// every shift and the alternatives can never be stacked.
/// </summary>
/// <remarks>
/// Why randomise: a fixed "+10% печать" node is bought without thinking by the twentieth round.
/// A slot that might offer "быстрее, но дороже по материалам" or "просто дешевле" is a decision,
/// and the department has to look at what the station actually needs this shift.
/// </remarks>
[Prototype("is14ResearchUpgradeGroup")]
public sealed partial class IS14ResearchUpgradeGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Hidden technologies, one of which is revealed at round start.</summary>
    [DataField(required: true)]
    public List<ProtoId<TechnologyPrototype>> Variants = new();

    /// <summary>Chance the slot appears at all. One means it is always there.</summary>
    [DataField]
    public float Chance = 1f;
}

/// <summary>
/// Hidden technologies this station's console will show. Filled at round start from the upgrade
/// groups, and later — phase 5 — by reverse-engineering contraband.
/// </summary>
[RegisterComponent]
public sealed partial class IS14RevealedTechComponent : Component
{
    [DataField]
    public HashSet<string> Revealed = new();
}

/// <summary>
/// Topics Gosplan declared a priority this shift: they cost less, and which ones they are
/// changes every round.
/// </summary>
/// <remarks>
/// The counterweight to a tree that is otherwise fixed. Structure stays predictable — the way
/// to a given item is always the same — while what is cheap this shift is not, so the map is
/// worth reading again instead of being walked from memory.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14ResearchPrioritiesComponent : Component
{
    /// <summary>Technology ID to the fraction knocked off its price, 0..1.</summary>
    [DataField]
    public Dictionary<string, float> Priorities = new();
}

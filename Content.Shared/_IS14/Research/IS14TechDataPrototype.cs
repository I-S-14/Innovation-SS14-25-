// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// Everything IS14 adds to a technology: what it costs in the five currencies and what it does
/// besides unlocking recipes. The prototype ID *is* the technology ID.
/// </summary>
/// <remarks>
/// A side table rather than fields on <see cref="TechnologyPrototype"/>, because that prototype
/// is not <c>IInheritingPrototype</c>: upstream technologies cannot be overridden by child
/// prototypes, and we do not want to edit upstream YAML. Technologies missing from this table
/// fall back to their upstream <c>cost</c>, rescaled and charged as scientific data.
/// </remarks>
[Prototype("is14Technology")]
public sealed partial class IS14TechDataPrototype : IPrototype
{
    /// <summary>The <see cref="TechnologyPrototype"/> this data belongs to.</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<ProtoId<ResearchPointTypePrototype>, int> Costs = new();

    /// <summary>
    /// What unlocking it changes on the station. Recipes stay in the technology's own
    /// <c>recipeUnlocks</c>; everything else lives here.
    /// </summary>
    [DataField]
    public List<IS14TechEffect> Effects = new();

    /// <summary>
    /// Free-text line for the console: what this research actually gives, in plain words.
    /// </summary>
    [DataField]
    public LocId? Summary;

    /// <summary>
    /// Sample this technology is paid for with, instead of data. Set, nothing unlocks it until
    /// the object has been destroyed in the analyzer — see <see cref="IS14BreakthroughRequirement"/>.
    /// </summary>
    [DataField]
    public IS14BreakthroughRequirement? Breakthrough;

    /// <summary>
    /// Technologies sharing a group are alternatives: researching one closes the others for the
    /// rest of the shift. A fork in the road the department has to argue about, and unlike a
    /// randomised slot it is on the map from minute one, so it can be planned for.
    /// </summary>
    [DataField]
    public string? ExclusiveGroup;

    /// <summary>
    /// Contraband that uncovers this topic. Set, the technology does not exist on any console
    /// until a confiscated sample of it has been taken apart on the reverse-engineering bench.
    /// </summary>
    /// <remarks>
    /// The same shape as a breakthrough on purpose, because it is the same bargain in a
    /// different coat: an object buys the <em>right to study</em> something rather than the
    /// study itself. The technology stays <c>hidden: true</c>, so until security hands the
    /// sample over there is nothing on the map to hint at it.
    /// </remarks>
    [DataField]
    public IS14BreakthroughRequirement? Contraband;
}

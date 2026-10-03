// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Research;

/// <summary>
/// One kind of research data the NIC collects. Every type has its own donor department
/// and its own way of being earned — see Docs/_IS14/research-design.md.
/// </summary>
/// <remarks>
/// Shared on purpose: the console draws names, colours and icons client-side, so the
/// server only ever ships numbers.
/// </remarks>
[Prototype("is14ResearchPointType")]
public sealed partial class ResearchPointTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Full name, used in the console header and in popups.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>One line explaining where this data comes from.</summary>
    [DataField]
    public LocId? Description;

    /// <summary>Three-letter tag for cost chips, where the full name never fits.</summary>
    [DataField(required: true)]
    public LocId ShortName;

    /// <summary>Department expected to feed this type. Display only.</summary>
    [DataField]
    public LocId? Donor;

    [DataField(required: true)]
    public Color Color;

    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>Order the counters appear in. Lower is further left.</summary>
    [DataField]
    public int SortOrder;
}

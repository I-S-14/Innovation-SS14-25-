// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.Organs;

/// <summary>
/// One thing a body is able to do because it has the organs for it.
/// </summary>
/// <remarks>
/// Organs never act on the body directly — they contribute to a faculty, and whatever cares
/// about that faculty asks for its level. That indirection is what lets two organs cover for
/// each other, lets a cybernetic replacement be nothing but a different contribution, and
/// keeps the circulation system from ever needing to know that lungs exist.
/// <para>
/// See <c>Docs/_IS14/organ-function-design.md</c>.
/// </para>
/// </remarks>
[Prototype("is14Faculty")]
public sealed partial class IS14FacultyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>What a doctor reads on the analyser.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Order in the readout. Ties fall back to ID.</summary>
    [DataField]
    public int Order;
}

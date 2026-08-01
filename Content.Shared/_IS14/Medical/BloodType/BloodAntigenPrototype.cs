// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// One marker on the surface of a blood cell. A blood type is nothing but a set of these,
/// and every rule in the system — compatibility, the readout on a test card, whether a
/// mismatch bites the first time or the second — is read off the antigens rather than off
/// a hardcoded list of eight groups.
/// </summary>
/// <remarks>
/// Adding an antigen prototype is enough to make it appear in tests and be checked during
/// transfusion. Nothing needs to learn its name.
/// </remarks>
[Prototype("bloodAntigen")]
public sealed partial class BloodAntigenPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Full name, for tooltips and prose.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>One or two characters, for the well on a test card.</summary>
    [DataField(required: true)]
    public LocId ShortName;

    /// <summary>Left-to-right order on a test card. Ties fall back to ID.</summary>
    [DataField]
    public int Order;

    /// <summary>
    /// Whether antibodies against this antigen exist without ever having met it.
    /// </summary>
    /// <remarks>
    /// True for A and B — a recipient rejects them on the first go. False for Rh, where the
    /// first exposure only teaches the body to recognise it and the second one is the one
    /// that hurts. That asymmetry is the whole reason this is a field and not a constant:
    /// it turns one careless transfusion into a debt that comes due later.
    /// </remarks>
    [DataField]
    public bool Preformed = true;
}

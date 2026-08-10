// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;

namespace Content.Server._IS14.Medical.BloodDonation;

/// <summary>
/// How much blood this person has already sold this shift.
/// </summary>
/// <remarks>
/// Lives on the donor rather than on the terminal so that a second donation point is not a
/// second quota — the station buys a fixed amount from a person, not from a machine. Added
/// the first time somebody is paid, so nobody who has never donated carries it.
/// </remarks>
[RegisterComponent]
public sealed partial class BloodDonorComponent : Component
{
    /// <summary>Units sold so far, counted against the terminal's quota.</summary>
    [DataField]
    public FixedPoint2 Sold;
}

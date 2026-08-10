// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;

namespace Content.Shared._IS14.Medical.IvDrip;

/// <summary>
/// Raised on a stand once blood has actually left a patient and reached the bag.
/// </summary>
/// <remarks>
/// The drip announces what it did and has no interest in who is listening. That is what
/// lets a donation bed keep a running total for a stand it has never heard of and does not
/// own — the two are related only by the patient they share.
/// </remarks>
[ByRefEvent]
public readonly record struct IvBloodDrawnEvent(EntityUid Patient, FixedPoint2 Amount);

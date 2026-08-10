// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodDonation;

/// <summary>
/// A bed that notices when somebody lying on it is hooked up to a drip, and keeps count of
/// what comes out.
/// </summary>
/// <remarks>
/// It owns no needle and no bag. The drip is an ordinary drip that a doctor wheels over and
/// attaches by hand, and the bed only ever learns about it through the patient they have in
/// common — which is exactly how a real one would work, and why any drip does.
/// <para>
/// The count is the reason the bed exists at all: blood in the bag could have come from
/// anywhere, so a bag is not proof of a donation. A running total that started when this
/// person lay down is.
/// </para>
/// </remarks>
[RegisterComponent]
public sealed partial class BloodDonationBedComponent : Component
{
    /// <summary>Port a donation console links to.</summary>
    [DataField]
    public ProtoId<SinkPortPrototype> LinkingPort = "IS14BloodDonationReceiver";

    /// <summary>The console watching this bed, if one has been linked.</summary>
    [DataField]
    public EntityUid? Console;

    /// <summary>
    /// Whose sitting this is. Kept after they get up so the doctor can still pay for blood
    /// already given — the money is a decision, and decisions take longer than standing up.
    /// </summary>
    [ViewVariables]
    public EntityUid? Donor;

    /// <summary>Blood taken from the current donor since they lay down.</summary>
    [ViewVariables]
    public FixedPoint2 Drawn;

    /// <summary>
    /// Whether this sitting has already been paid out, so the button cannot be pressed twice.
    /// </summary>
    [ViewVariables]
    public bool Paid;
}

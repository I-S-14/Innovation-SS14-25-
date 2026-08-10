// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._IS14.Economy;
using Content.Shared.DeviceLinking;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodDonation;

/// <summary>
/// The doctor's end of a donation: a screen showing what is coming out of the donor on the
/// linked bed, a button to stop it, and a button that prints what the station owes them.
/// </summary>
/// <remarks>
/// Everything this console does, a doctor could do by hand — read the drip, pull the needle,
/// walk to a terminal. What it adds is that all three are in one place while the blood is
/// still flowing, which is the difference between supervising a donation and finding out
/// afterwards how it went.
/// </remarks>
[RegisterComponent]
public sealed partial class BloodDonationConsoleComponent : Component
{
    /// <summary>Port the bed links to.</summary>
    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "IS14BloodDonationSender";

    /// <summary>The bed this console is watching, if one has been linked.</summary>
    [DataField]
    public EntityUid? Bed;

    /// <summary>Credits paid per unit of blood the donor gave.</summary>
    /// <remarks>
    /// Flat, and deliberately not scaled by how rare the donor's group is. Paying more for
    /// O− would be good economics and terrible design: the payout would announce the group,
    /// and a console that types your blood for free is a console that retires both the
    /// analyser and the test strip.
    /// <para>
    /// Priced against the bottom of the payroll rather than the top: a full quota comes to
    /// roughly two hours of a passenger's wage, which is real money to the people who have
    /// nothing else to sell and a rounding error to a department head.
    /// </para>
    /// </remarks>
    [DataField]
    public int CreditsPerUnit = 2;

    /// <summary>
    /// The range the doctor may set the rate to.
    /// </summary>
    /// <remarks>
    /// Bounded rather than free, because the rate is spendable department money in a box
    /// anybody with medical access can open. The floor stops a doctor from buying blood for
    /// nothing; the ceiling stops the console from becoming a payroll of its own. Inside
    /// those two numbers it is a real decision — pay over the odds to get O− donors through
    /// the door, or pay the minimum and keep the budget for supplies.
    /// </remarks>
    [DataField]
    public int MinCreditsPerUnit = 1;

    /// <inheritdoc cref="MinCreditsPerUnit"/>
    [DataField]
    public int MaxCreditsPerUnit = 6;

    /// <summary>
    /// Whether the console pulls the needle by itself once the donor reaches
    /// <see cref="WarnBloodLevel"/>.
    /// </summary>
    /// <remarks>
    /// On by default, and a switch rather than a rule: the ordinary donation is one a doctor
    /// should be able to set going and walk away from, but a doctor who is standing right
    /// there and knows what they are doing should not have the machine overrule them. Off,
    /// the console goes back to warning and nothing else.
    /// </remarks>
    [DataField]
    public bool AutoStop = true;

    /// <summary>Receipt printed alongside the cash. Null prints nothing.</summary>
    /// <remarks>
    /// The paper is what makes the handover checkable by somebody who was not there. Cash
    /// alone says nothing about who gave how much for what rate; a receipt in the donor's
    /// pocket is evidence, and evidence is what turns short-changing them into something
    /// that can be argued about afterwards.
    /// </remarks>
    [DataField]
    public EntProtoId? ReceiptPrototype = "IS14BloodDonationReceipt";

    /// <summary>Played when the receipt comes out.</summary>
    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>
    /// How much blood the station will pay one person for per shift.
    /// </summary>
    /// <remarks>
    /// Blood grows back, so without a ceiling a willing donor and a patient doctor are a
    /// money printer. The cap bounds the payout and not the needle: a doctor may keep
    /// drawing past it, they simply cannot bill the station for it.
    /// </remarks>
    [DataField]
    public FixedPoint2 Quota = FixedPoint2.New(60);

    /// <summary>Blood level, as a fraction of full, below which the console flags the donor.</summary>
    /// <remarks>
    /// A warning and nothing more, because the doctor is the one deciding when to stop —
    /// that is the entire point of putting them at a screen instead of automating them
    /// away. Set to the upstream <c>BloodstreamComponent.BloodlossThreshold</c>, so the
    /// console starts complaining exactly where bloodloss damage begins.
    /// </remarks>
    [DataField]
    public float WarnBloodLevel = 0.9f;

    /// <summary>
    /// Whether the station refuses to pay for blood from a donor who is not clean.
    /// </summary>
    /// <remarks>
    /// Gates the money rather than the needle. A doctor with a reason to bleed somebody full
    /// of painkillers is welcome to; the station simply will not buy the result, because
    /// blood carries whatever was in the donor and a transfusion of it is a dose nobody
    /// ordered.
    /// </remarks>
    [DataField]
    public bool RequireFasting = true;

    /// <summary>How much foreign reagent is written off as traces rather than a dose.</summary>
    [DataField]
    public FixedPoint2 FastingTolerance = FixedPoint2.New(0.5);

    /// <summary>Budget the money comes out of.</summary>
    /// <remarks>
    /// Medbay by default, because medbay is who wants the blood. It matters that this is a
    /// real account and not a spawner: a department that has spent its budget cannot buy
    /// blood, so stocking up early is a decision with a price.
    /// </remarks>
    [DataField]
    public ProtoId<StationAccountPrototype> Account = "StationMedical";

    /// <summary>Cash printed for the donor.</summary>
    /// <remarks>
    /// Paper rather than a transfer on purpose. It puts the doctor in the middle of the
    /// transaction holding something they have to hand over, which is a moment where they
    /// can be short-changed, robbed, or generous — none of which exists when a number moves
    /// between two accounts nobody is standing next to.
    /// </remarks>
    [DataField]
    public ProtoId<StackPrototype> CashStackType = "IS14Credit";

    /// <summary>Played when the cash is printed.</summary>
    [DataField]
    public SoundSpecifier? PayoutSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}

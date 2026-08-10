// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._IS14.Economy;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Medical.BloodDonation;
using Content.Shared._IS14.Medical.BloodType;
using Content.Shared._IS14.Medical.IvDrip;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.BloodDonation;

/// <summary>
/// Watching a donation and paying for it.
/// </summary>
/// <remarks>
/// Three separate objects, on purpose. The drip is the ordinary drip and does all the actual
/// work; the bed knows only what came out of whoever is lying on it; the console knows only
/// what the bed tells it and what the station owes. None of them can do the job alone, and
/// none of them had to be invented for it — the drip in particular is untouched beyond
/// announcing what it drew.
/// <para>
/// Nothing here stops a draw on its own. A doctor is standing at the screen, and taking that
/// decision off them would make the screen pointless; the machine's only refusals are about
/// money, which is the one thing a doctor cannot simply do by hand.
/// </para>
/// </remarks>
public sealed class BloodDonationSystem : EntitySystem
{
    [Dependency] private readonly BloodTypeSystem _bloodType = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedIvDripSystem _drip = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>How often an open console redraws itself.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodDonationConsoleComponent, NewLinkEvent>(OnConsoleLinked);
        SubscribeLocalEvent<BloodDonationConsoleComponent, PortDisconnectedEvent>(OnConsoleUnlinked);
        SubscribeLocalEvent<BloodDonationConsoleComponent, LinkAttemptEvent>(OnConsoleLinkAttempt);
        SubscribeLocalEvent<BloodDonationBedComponent, NewLinkEvent>(OnBedLinked);
        SubscribeLocalEvent<BloodDonationBedComponent, PortDisconnectedEvent>(OnBedUnlinked);
        SubscribeLocalEvent<BloodDonationBedComponent, LinkAttemptEvent>(OnBedLinkAttempt);

        SubscribeLocalEvent<BloodDonationBedComponent, StrappedEvent>(OnStrapped);

        // Subscribed on the drip rather than on the bed: the stand is what announces a draw,
        // and it has no idea a bed is involved. The patient they share is the only link
        // between the two, and the only one either of them should need.
        SubscribeLocalEvent<IvDripComponent, IvBloodDrawnEvent>(OnBloodDrawn);

        SubscribeLocalEvent<BloodDonationConsoleComponent, BloodDonationStopMessage>(OnStopPressed);
        SubscribeLocalEvent<BloodDonationConsoleComponent, BloodDonationPayoutMessage>(OnPayoutPressed);
        SubscribeLocalEvent<BloodDonationConsoleComponent, BloodDonationSetRateMessage>(OnRateSet);
        SubscribeLocalEvent<BloodDonationConsoleComponent, BloodDonationAutoStopMessage>(OnAutoStopSet);
    }

    // ── Linking ───────────────────────────────────────────────────────────────

    private void OnConsoleLinked(Entity<BloodDonationConsoleComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != ent.Comp.LinkingPort || !HasComp<BloodDonationBedComponent>(args.Sink))
            return;

        ent.Comp.Bed = args.Sink;
    }

    private void OnBedLinked(Entity<BloodDonationBedComponent> ent, ref NewLinkEvent args)
    {
        if (args.SinkPort != ent.Comp.LinkingPort || !HasComp<BloodDonationConsoleComponent>(args.Source))
            return;

        ent.Comp.Console = args.Source;
    }

    private void OnConsoleUnlinked(Entity<BloodDonationConsoleComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port == ent.Comp.LinkingPort)
            ent.Comp.Bed = null;
    }

    private void OnBedUnlinked(Entity<BloodDonationBedComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port == ent.Comp.LinkingPort)
            ent.Comp.Console = null;
    }

    /// <summary>One bed per console and one console per bed: the readout names a single donor.</summary>
    private void OnConsoleLinkAttempt(Entity<BloodDonationConsoleComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.Bed != null)
            args.Cancel();
    }

    private void OnBedLinkAttempt(Entity<BloodDonationBedComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.Console != null)
            args.Cancel();
    }

    // ── The sitting ───────────────────────────────────────────────────────────

    /// <summary>
    /// A new body on the bed is a new sitting, and the old one's count goes with it.
    /// </summary>
    /// <remarks>
    /// Unpaid blood is forfeited here rather than carried over, because a total spanning two
    /// donors is a total that pays the wrong one. The console says plainly how much is owed
    /// while the donor is still lying there; letting the next person up is the doctor
    /// deciding they were finished.
    /// </remarks>
    private void OnStrapped(Entity<BloodDonationBedComponent> ent, ref StrappedEvent args)
    {
        ent.Comp.Donor = args.Buckle.Owner;
        ent.Comp.Drawn = FixedPoint2.Zero;
        ent.Comp.Paid = false;
    }

    /// <summary>Counts a draw against whichever donation bed the patient happens to be lying on.</summary>
    private void OnBloodDrawn(Entity<IvDripComponent> ent, ref IvBloodDrawnEvent args)
    {
        if (!TryComp<BuckleComponent>(args.Patient, out var buckle)
            || buckle.BuckledTo is not { } strap
            || !TryComp<BloodDonationBedComponent>(strap, out var bed)
            || bed.Donor != args.Patient)
        {
            return;
        }

        bed.Drawn += args.Amount;
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    /// <summary>Pulls the needle on the doctor's say-so.</summary>
    private void OnStopPressed(Entity<BloodDonationConsoleComponent> ent, ref BloodDonationStopMessage args)
    {
        if (!TryGetBed(ent, out var bed)
            || bed.Value.Comp.Donor is not { } donor
            || !TryGetDrip(donor, out var drip))
        {
            return;
        }

        _drip.Detach(drip.Value);
        UpdateUi(ent);
    }

    /// <summary>
    /// Sets what the station pays per unit, clamped on arrival.
    /// </summary>
    /// <remarks>
    /// Clamped here rather than trusted from the window, because a bound-UI message is
    /// whatever the client chose to send and the range is the only thing standing between
    /// this console and the department's whole budget.
    /// </remarks>
    private void OnRateSet(Entity<BloodDonationConsoleComponent> ent, ref BloodDonationSetRateMessage args)
    {
        ent.Comp.CreditsPerUnit = Math.Clamp(args.Rate, ent.Comp.MinCreditsPerUnit, ent.Comp.MaxCreditsPerUnit);
        UpdateUi(ent);
    }

    /// <summary>Arms or disarms the safety cutoff.</summary>
    private void OnAutoStopSet(Entity<BloodDonationConsoleComponent> ent, ref BloodDonationAutoStopMessage args)
    {
        ent.Comp.AutoStop = args.Enabled;
        UpdateUi(ent);
    }

    /// <summary>
    /// Prints what the station owes and puts it in the doctor's hand for them to pass on.
    /// </summary>
    private void OnPayoutPressed(Entity<BloodDonationConsoleComponent> ent, ref BloodDonationPayoutMessage args)
    {
        if (!TryGetBed(ent, out var bed) || bed.Value.Comp.Donor is not { } donor)
            return;

        // Re-derived rather than trusted from the state the client was looking at, which may
        // be a second stale and is in any case the client's.
        if (GetPayBlocker(ent, bed.Value, donor) != null)
            return;

        var paidFor = FixedPoint2.Min(bed.Value.Comp.Drawn, GetRemainingQuota(ent, donor));
        var price = (paidFor * ent.Comp.CreditsPerUnit).Int();

        if (price <= 0)
            return;

        if (!TryDebitStation(ent, price, donor, paidFor))
        {
            _popup.PopupEntity(
                Loc.GetString("is14-blood-donation-console-no-funds"),
                ent,
                args.Actor,
                PopupType.MediumCaution);
            return;
        }

        bed.Value.Comp.Paid = true;
        EnsureComp<BloodDonorComponent>(donor).Sold += paidFor;

        var cash = _stack.SpawnAtPosition(price, ent.Comp.CashStackType, Transform(ent.Owner).Coordinates);

        // Into the doctor's hand where it will go: the point of paying in paper is that
        // somebody has to carry it across the room and hand it over.
        _hands.TryPickupAnyHand(args.Actor, cash);

        _audio.PlayPvs(ent.Comp.PayoutSound, ent.Owner);
        PrintReceipt(ent, args.Actor, donor, paidFor, price);
        UpdateUi(ent);
    }

    /// <summary>
    /// Prints the paperwork that goes with the cash.
    /// </summary>
    /// <remarks>
    /// Records the rate as well as the total, because the rate is the part a doctor can set
    /// and therefore the part worth being able to check afterwards. Cash on its own says
    /// only that somebody was paid something.
    /// </remarks>
    private void PrintReceipt(
        Entity<BloodDonationConsoleComponent> ent,
        EntityUid user,
        EntityUid donor,
        FixedPoint2 volume,
        int price)
    {
        if (ent.Comp.ReceiptPrototype is not { } proto)
            return;

        var receipt = Spawn(proto, Transform(ent.Owner).Coordinates);

        _paper.SetContent(receipt, Loc.GetString(
            "is14-blood-donation-receipt-content",
            ("donor", Identity.Name(donor, EntityManager)),
            ("volume", volume),
            ("rate", ent.Comp.CreditsPerUnit),
            ("total", price)));

        _hands.TryPickupAnyHand(user, receipt);
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
    }

    // ── Money ─────────────────────────────────────────────────────────────────

    /// <summary>Units the station will still pay this person for this shift.</summary>
    private FixedPoint2 GetRemainingQuota(Entity<BloodDonationConsoleComponent> ent, EntityUid donor)
    {
        var sold = CompOrNull<BloodDonorComponent>(donor)?.Sold ?? FixedPoint2.Zero;
        return FixedPoint2.Max(FixedPoint2.Zero, ent.Comp.Quota - sold);
    }

    /// <summary>Takes the payout out of the department budget and files it with the monitor.</summary>
    private bool TryDebitStation(
        Entity<BloodDonationConsoleComponent> ent,
        int price,
        EntityUid donor,
        FixedPoint2 volume)
    {
        if (_station.GetOwningStation(ent.Owner) is not { } station
            || !_stationBank.TryChangeStationBalance(station, ent.Comp.Account, -price, out var balance))
        {
            return false;
        }

        if (_stationBank.GetStationAccount(station, ent.Comp.Account) is { } account)
        {
            RaiseLocalEvent(new EconomyTransactionEvent(
                account.AccountNumber,
                -price,
                balance,
                Loc.GetString(
                    "economy-transaction-blood-donation",
                    ("volume", volume),
                    ("donor", Identity.Name(donor, EntityManager))),
                ent.Owner));
        }

        return true;
    }

    /// <summary>
    /// Why the payout button is dead, or null if it is live.
    /// </summary>
    private string? GetPayBlocker(
        Entity<BloodDonationConsoleComponent> ent,
        Entity<BloodDonationBedComponent> bed,
        EntityUid donor)
    {
        if (bed.Comp.Paid)
            return "is14-blood-donation-console-block-paid";

        if (bed.Comp.Drawn <= 0)
            return "is14-blood-donation-console-block-nothing";

        if (GetRemainingQuota(ent, donor) <= 0)
            return "is14-blood-donation-console-block-quota";

        if (ent.Comp.RequireFasting && !IsClean(ent, donor))
            return "is14-blood-donation-console-block-tainted";

        return null;
    }

    /// <summary>
    /// Whether the donor has nothing in them but their own blood.
    /// </summary>
    /// <remarks>
    /// One scan covers both ways of being dirty, because a mob keeps them in one place: the
    /// bloodstream solution an injector fills is the same solution the blood is in. So
    /// anything in there that is not the donor's own blood is either a drug still circulating
    /// or blood that is not theirs — hemolysate from a bad transfusion, another species from
    /// a good one — and all of it would otherwise end up in the bag.
    /// </remarks>
    private bool IsClean(Entity<BloodDonationConsoleComponent> ent, EntityUid donor)
    {
        if (!TryComp<BloodstreamComponent>(donor, out var bloodstream)
            || _bloodType.GetBloodReagent((donor, bloodstream)) is not { } own
            || !_solutions.ResolveSolution(donor, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
        {
            return false;
        }

        var foreign = FixedPoint2.Zero;

        foreach (var (reagent, quantity) in blood.Contents)
        {
            if (reagent.Prototype != own)
                foreign += quantity;
        }

        return foreign <= ent.Comp.FastingTolerance;
    }

    // ── Readout ───────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<BloodDonationConsoleComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            Entity<BloodDonationConsoleComponent> console = (uid, comp);

            // Runs whether or not anybody is looking at the screen. A cutoff that only works
            // while the window is open is not a cutoff — the whole reason to arm it is so the
            // doctor can go and do something else.
            TryAutoStop(console);

            if (_ui.IsUiOpen(uid, BloodDonationConsoleUiKey.Key))
                UpdateUi(console);
        }
    }

    /// <summary>
    /// Pulls the needle once the donor reaches the safety line, if the doctor armed it.
    /// </summary>
    /// <remarks>
    /// Checked on the same one-second beat as the readout, so a donor can end up a tick past
    /// the line before the needle comes out — a couple of units on a three-hundred unit
    /// bloodstream. Tightening that would mean running this every frame to buy back a
    /// fraction of a percent, which is not a trade worth making.
    /// </remarks>
    private void TryAutoStop(Entity<BloodDonationConsoleComponent> ent)
    {
        if (!ent.Comp.AutoStop
            || !TryGetBed(ent, out var bed)
            || bed.Value.Comp.Donor is not { } donor
            || TerminatingOrDeleted(donor)
            || !TryGetDrip(donor, out var drip)
            || drip.Value.Comp.Mode != IvDripMode.Draw)
        {
            return;
        }

        if (_bloodstream.GetBloodLevel(donor) >= ent.Comp.WarnBloodLevel)
            return;

        _drip.Detach(drip.Value);

        // Said at the donor rather than at the console: the person it happened to is the one
        // who needs to know, and anybody standing over them can read it too.
        _popup.PopupEntity(Loc.GetString("is14-blood-donation-console-auto-stopped"), donor);
    }

    private void UpdateUi(Entity<BloodDonationConsoleComponent> ent)
    {
        _ui.SetUiState(ent.Owner, BloodDonationConsoleUiKey.Key, BuildState(ent));
    }

    private BloodDonationConsoleUiState BuildState(Entity<BloodDonationConsoleComponent> ent)
    {
        if (!TryGetBed(ent, out var bed))
            return Blank(ent, BloodDonationState.Unlinked);

        if (bed.Value.Comp.Donor is not { } donor || TerminatingOrDeleted(donor))
            return Blank(ent, BloodDonationState.Empty);

        var drawn = bed.Value.Comp.Drawn;
        var quotaLeft = GetRemainingQuota(ent, donor);
        var blocker = GetPayBlocker(ent, bed.Value, donor);
        var bloodLevel = _bloodstream.GetBloodLevel(donor);

        var packVolume = 0f;
        var packCapacity = 0f;
        var state = BloodDonationState.Waiting;
        var hasDrip = TryGetDrip(donor, out var drip);

        if (hasDrip)
        {
            state = drip!.Value.Comp.Flowing && drip.Value.Comp.Mode == IvDripMode.Draw
                ? BloodDonationState.Drawing
                : BloodDonationState.Stalled;

            if (_drip.GetPackSolution(drip.Value) is { } pack)
            {
                packVolume = pack.Volume.Float();
                packCapacity = pack.MaxVolume.Float();
            }
        }

        return new BloodDonationConsoleUiState(
            state,
            Identity.Name(donor, EntityManager),
            bloodLevel,
            bloodLevel < ent.Comp.WarnBloodLevel,
            drawn.Float(),
            packVolume,
            packCapacity,
            !ent.Comp.RequireFasting || IsClean(ent, donor),
            quotaLeft.Float(),
            (FixedPoint2.Min(drawn, quotaLeft) * ent.Comp.CreditsPerUnit).Int(),
            blocker == null,
            blocker == null ? string.Empty : Loc.GetString(blocker),
            hasDrip,
            ent.Comp.CreditsPerUnit,
            ent.Comp.MinCreditsPerUnit,
            ent.Comp.MaxCreditsPerUnit,
            ent.Comp.AutoStop,
            ent.Comp.WarnBloodLevel);
    }

    /// <summary>
    /// The screen with no donor on it. The settings still travel, because the rate and the
    /// cutoff are the console's own and a doctor sets them up before anybody lies down.
    /// </summary>
    private static BloodDonationConsoleUiState Blank(
        Entity<BloodDonationConsoleComponent> ent,
        BloodDonationState state)
        => new(state, string.Empty, 0f, false, 0f, 0f, 0f, true, 0f, 0, false, string.Empty, false,
            ent.Comp.CreditsPerUnit, ent.Comp.MinCreditsPerUnit, ent.Comp.MaxCreditsPerUnit, ent.Comp.AutoStop,
            ent.Comp.WarnBloodLevel);

    // ── Lookups ───────────────────────────────────────────────────────────────

    private bool TryGetBed(
        Entity<BloodDonationConsoleComponent> ent,
        [NotNullWhen(true)] out Entity<BloodDonationBedComponent>? bed)
    {
        bed = null;

        if (ent.Comp.Bed is not { } uid || !TryComp<BloodDonationBedComponent>(uid, out var comp))
            return false;

        bed = (uid, comp);
        return true;
    }

    /// <summary>
    /// The stand currently stuck into this person, if any.
    /// </summary>
    /// <remarks>
    /// Read off the needle rather than searched for. The patient's half of a drip already
    /// names the stand it belongs to, so "is this donor hooked up" is a component lookup and
    /// not a sweep of the room.
    /// </remarks>
    private bool TryGetDrip(EntityUid donor, [NotNullWhen(true)] out Entity<IvDripComponent>? drip)
    {
        drip = null;

        if (!TryComp<IvNeedleComponent>(donor, out var needle)
            || !TryComp<IvDripComponent>(needle.Drip, out var comp))
        {
            return false;
        }

        drip = (needle.Drip, comp);
        return true;
    }
}

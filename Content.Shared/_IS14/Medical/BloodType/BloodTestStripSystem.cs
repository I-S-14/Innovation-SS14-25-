// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// The paper strip: soaking it, developing it, writing on it and reading it back.
/// </summary>
/// <remarks>
/// The card never names a group anywhere — not on examine, not in the window it opens, and
/// not in what it stores. It keeps the antigens it found and nothing else, so the player gets
/// three wells that either clotted or did not. Turning that into "B negative" is the doctor's
/// job, and it is the whole reason this exists next to an analyser that would just say it.
/// </remarks>
public sealed class BloodTestStripSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    /// <summary>Longest name the card has room for.</summary>
    private const int PatientLimit = 32;

    [Dependency] private readonly BloodTypeSystem _bloodType = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodTestStripComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<BloodTestStripComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BloodTestStripComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<BloodTestStripComponent, BloodStripSetPatientMessage>(OnSetPatient);
        SubscribeLocalEvent<BloodTestStripComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>Whether the reaction has finished and the card can be read.</summary>
    public bool IsDeveloped(BloodTestStripComponent comp)
    {
        return comp.Used && _timing.CurTime >= comp.DevelopAt;
    }

    /// <summary>
    /// Whether the blood on the card is a kind these antibodies know. A card is printed for
    /// one species, and a moth bleeding on a human one gets three wells of nothing — which
    /// must not be shown as three negatives, because three negatives means O-negative.
    /// </summary>
    public bool IsMismatched(BloodTestStripComponent comp)
    {
        return comp.Used && comp.Reagent != null && comp.Reagent.Value != comp.Card;
    }

    // ── Soaking ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Blood landing on a blank card commits it. Anything arriving afterwards is ignored:
    /// the card reads the first sample, which means a second donor's blood cannot quietly
    /// rewrite an answer somebody has already acted on.
    /// </summary>
    private void OnSolutionChanged(Entity<BloodTestStripComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (ent.Comp.Used || _net.IsClient || args.SolutionId != ent.Comp.Solution)
            return;

        var blood = FixedPoint2.Zero;

        foreach (var (reagent, quantity) in args.Solution.Contents)
        {
            if (_bloodType.IsBloodReagent(reagent.Prototype))
                blood += quantity;
        }

        if (blood < ent.Comp.Required || !_bloodType.TryGetSample(ent.Owner, out var read))
            return;

        ent.Comp.Used = true;
        ent.Comp.Reagent = read.Reagent;
        ent.Comp.Antigens = new HashSet<ProtoId<BloodAntigenPrototype>>(read.Antigens);
        ent.Comp.DevelopAt = _timing.CurTime + ent.Comp.DevelopDelay;
        Dirty(ent);

        // The stain is immediate even though the reading is not — the paper is wet the moment
        // you bleed on it, and a spent card should look spent right away.
        _appearance.SetData(ent.Owner, BloodTestStripVisuals.Used, true);
    }

    // ── Writing on it ─────────────────────────────────────────────────────────

    /// <summary>
    /// A pen unlocks the name field, the same bargain a sheet of paper makes. Clicking the
    /// card with anything else falls through, so a syringe still bleeds on it.
    /// </summary>
    private void OnInteractUsing(Entity<BloodTestStripComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tags.HasTag(args.Used, WriteTag))
            return;

        args.Handled = true;

        // A signed card stays signed. The pen still opens it — you are allowed to read what
        // you wrote — but the blank does not come back, so nobody relabels a strip after the
        // fact to make it agree with whatever they have decided since.
        if (IsSigned(ent.Comp))
        {
            _popup.PopupClient(Loc.GetString("is14-blood-strip-already-signed"), ent.Owner, args.User);
        }
        else
        {
            ent.Comp.Writable = true;
            Dirty(ent);
        }

        _ui.TryOpenUi(ent.Owner, BloodTestStripUiKey.Key, args.User);
    }

    /// <summary>Whether a name has been committed to this card.</summary>
    public bool IsSigned(BloodTestStripComponent comp)
    {
        return comp.Patient != string.Empty;
    }

    /// <summary>
    /// The pen goes away with the window. Otherwise a card left open once would stay writable
    /// for anybody who picked it up later.
    /// </summary>
    private void OnUiClosed(Entity<BloodTestStripComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!ent.Comp.Writable || _ui.IsUiOpen(ent.Owner, BloodTestStripUiKey.Key))
            return;

        ent.Comp.Writable = false;
        Dirty(ent);
    }

    /// <summary>
    /// Signing the card, once and for good.
    /// </summary>
    /// <remarks>
    /// Every guard is repeated here rather than trusted from the window, which can be told to
    /// send this at any moment with anything in it. The permanence in particular has to live
    /// on this side: a name that could be rewritten later would make a signed strip worth
    /// nothing, and that is the only reason to write on one at all.
    /// </remarks>
    private void OnSetPatient(Entity<BloodTestStripComponent> ent, ref BloodStripSetPatientMessage args)
    {
        if (IsSigned(ent.Comp))
        {
            _popup.PopupClient(Loc.GetString("is14-blood-strip-already-signed"), ent.Owner, args.Actor);
            return;
        }

        if (!ent.Comp.Writable || !HasPen(args.Actor))
        {
            _popup.PopupClient(Loc.GetString("is14-blood-strip-need-pen"), ent.Owner, args.Actor);
            return;
        }

        var patient = args.Patient.Trim();

        if (patient.Length > PatientLimit)
            patient = patient[..PatientLimit];

        // An empty line is somebody pressing enter on nothing. Committing that would burn the
        // card's one blank on a name nobody typed.
        if (patient == string.Empty)
            return;

        ent.Comp.Patient = patient;
        ent.Comp.Writable = false;
        Dirty(ent);
    }

    private bool HasPen(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (_tags.HasTag(held, WriteTag))
                return true;
        }

        return false;
    }

    // ── Reading ───────────────────────────────────────────────────────────────

    private void OnExamined(Entity<BloodTestStripComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(BloodTestStripComponent)))
        {
            if (ent.Comp.Patient != string.Empty)
                args.PushMarkup(Loc.GetString("is14-blood-strip-examine-patient", ("patient", ent.Comp.Patient)));

            if (!ent.Comp.Used || ent.Comp.Reagent is not { } reagent)
            {
                args.PushMarkup(Loc.GetString("is14-blood-strip-examine-blank"));
                return;
            }

            if (IsMismatched(ent.Comp))
            {
                args.PushMarkup(Loc.GetString("is14-blood-strip-examine-nothing"));
                return;
            }

            if (!IsDeveloped(ent.Comp))
            {
                args.PushMarkup(Loc.GetString("is14-blood-strip-examine-developing"));
                return;
            }

            var wells = _bloodType.BuildWells(new BloodSample(reagent, ent.Comp.Antigens, null));

            args.PushMarkup(wells.Count == 0
                ? Loc.GetString("is14-blood-strip-examine-nothing")
                : Loc.GetString("is14-blood-strip-examine-result", ("wells", FormatWells(wells))));
        }
    }

    /// <summary>Wells as one line of prose, for reading the card without opening it.</summary>
    private string FormatWells(List<BloodTestWellState> wells)
    {
        var builder = new StringBuilder();

        foreach (var well in wells)
        {
            if (builder.Length > 0)
                builder.Append("  ");

            builder.Append(Loc.GetString(
                well.Positive ? "is14-blood-strip-well-positive" : "is14-blood-strip-well-negative",
                ("antigen", well.ShortName)));
        }

        return builder.ToString();
    }
}

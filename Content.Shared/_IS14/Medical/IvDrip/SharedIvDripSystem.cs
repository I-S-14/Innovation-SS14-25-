// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._IS14.Cord;
using Content.Shared._IS14.Medical.BloodType;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.IvDrip;

/// <summary>
/// Getting a needle into somebody and back out again. The fluid itself is not this
/// system's problem — that runs on a clock on the server — but everything that decides
/// whether the needle is in, and who is allowed to take it out, is here.
/// </summary>
/// <remarks>
/// Two people can end a transfusion and they reach it differently. The doctor clicks the
/// stand, which is the thing they can see from where they are standing. The patient
/// clicks the alert, because from inside the mob the needle is the object, not the pole.
/// </remarks>
public abstract class SharedIvDripSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedCordSystem _cord = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IvDripComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<IvDripComponent, CanDropDraggedEvent>(OnCanDropDragged);
        SubscribeLocalEvent<IvDripComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<IvDripComponent, IvDripAttachDoAfterEvent>(OnAttachDoAfter);
        SubscribeLocalEvent<IvDripComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<IvDripComponent, GetVerbsEvent<AlternativeVerb>>(OnDripAltVerbs);
        SubscribeLocalEvent<IvDripComponent, ExaminedEvent>(OnDripExamined);
        SubscribeLocalEvent<IvDripComponent, EntInsertedIntoContainerMessage>(OnPackInserted);
        SubscribeLocalEvent<IvDripComponent, EntRemovedFromContainerMessage>(OnPackRemoved);
        SubscribeLocalEvent<IvDripComponent, ComponentShutdown>(OnDripShutdown);

        SubscribeLocalEvent<IvNeedleComponent, IvNeedleRemoveAlertEvent>(OnNeedleAlert);
        SubscribeLocalEvent<IvNeedleComponent, IvDripDetachDoAfterEvent>(OnDetachDoAfter);
        SubscribeLocalEvent<IvNeedleComponent, ComponentShutdown>(OnNeedleShutdown);
    }

    // ── Putting it in ─────────────────────────────────────────────────────────

    private void OnCanDrag(Entity<IvDripComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    /// <summary>
    /// A drip only goes into something with blood in it, and only one patient at a time —
    /// the second needle would have nowhere to come from.
    /// </summary>
    private void OnCanDropDragged(Entity<IvDripComponent> ent, ref CanDropDraggedEvent args)
    {
        args.CanDrop = ent.Comp.Patient == null
                       && args.Target != ent.Owner
                       && HasComp<BloodstreamComponent>(args.Target)
                       && HasComp<MobStateComponent>(args.Target)
                       && !HasComp<IvNeedleComponent>(args.Target);

        args.Handled = true;
    }

    private void OnDragDropDragged(Entity<IvDripComponent> ent, ref DragDropDraggedEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartAttach(ent, args.User, args.Target);
    }

    private bool TryStartAttach(Entity<IvDripComponent> ent, EntityUid user, EntityUid target)
    {
        if (ent.Comp.Patient != null || !HasComp<BloodstreamComponent>(target))
            return false;

        // One needle per patient. A second stand would quietly take the first one's
        // needle off them, leaving it pumping into somebody it is no longer attached to.
        if (HasComp<IvNeedleComponent>(target))
        {
            _popup.PopupClient(
                Loc.GetString("is14-iv-drip-already-needled", ("target", Identity.Entity(target, EntityManager))),
                ent,
                user);
            return false;
        }

        // A bagless stand can still take blood, but it has nowhere to put it, and a
        // needle that does nothing is worse than no needle.
        if (GetPack(ent) == null)
        {
            _popup.PopupClient(Loc.GetString("is14-iv-drip-no-pack"), ent, user);
            return false;
        }

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager,
                user,
                ent.Comp.AttachDelay,
                new IvDripAttachDoAfterEvent(),
                ent.Owner,
                target: target,
                used: ent.Owner)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            }))
        {
            return false;
        }

        _popup.PopupClient(
            Loc.GetString("is14-iv-drip-attach-attempt", ("target", Identity.Entity(target, EntityManager))),
            target,
            user);

        return true;
    }

    private void OnAttachDoAfter(Entity<IvDripComponent> ent, ref IvDripAttachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        args.Handled = Attach(ent, target);
    }

    // ── Taking it out ─────────────────────────────────────────────────────────

    /// <summary>
    /// The doctor's way out. Bare-handed only: a click with something in hand is somebody
    /// hanging a bag, and eating that would make the stand hard to load.
    /// </summary>
    private void OnInteractHand(Entity<IvDripComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Patient == null)
            return;

        Detach(ent);
        args.Handled = true;
    }

    /// <summary>
    /// The patient's way out. It costs them a moment rather than being instant, which is
    /// the difference between refusing treatment and flinching.
    /// </summary>
    private void OnNeedleAlert(Entity<IvNeedleComponent> ent, ref IvNeedleRemoveAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            ent.Owner,
            GetDetachDelay(ent),
            new IvDripDetachDoAfterEvent(),
            ent.Owner)
        {
            BreakOnDamage = true,
        });
    }

    private void OnDetachDoAfter(Entity<IvNeedleComponent> ent, ref IvDripDetachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryComp<IvDripComponent>(ent.Comp.Drip, out var drip))
            return;

        Detach((ent.Comp.Drip, drip));
        args.Handled = true;
    }

    // ── Verbs ─────────────────────────────────────────────────────────────────

    private void OnDripAltVerbs(Entity<IvDripComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var draw = ent.Comp.Mode == IvDripMode.Draw;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(draw ? "is14-iv-drip-verb-mode-inject" : "is14-iv-drip-verb-mode-draw"),
            Act = () => SetMode(ent, draw ? IvDripMode.Inject : IvDripMode.Draw, user),
        });

        // Sorted by size rather than alphabetically, so the menu reads as a dial from slow
        // to fast instead of as an unordered list of numbers.
        var priority = 0;

        foreach (var amount in ent.Comp.TransferAmounts)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("is14-iv-drip-verb-rate", ("amount", amount)),
                Category = VerbCategory.SetTransferAmount,
                Disabled = amount == ent.Comp.TransferAmount,
                Act = () => SetTransferAmount(ent, amount, user),
                Priority = priority--,
            });
        }

        if (GetPack(ent) == null || !_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var slot))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("is14-iv-drip-verb-take-pack"),
            Act = () => _itemSlots.TryEjectToHands(ent.Owner, slot, user, excludeUserAudio: true),
        });
    }

    /// <summary>
    /// Opens the valve wider or narrower. Takes effect on the next tick rather than
    /// immediately, so turning it up is not a way to dump the bag in one go.
    /// </summary>
    public void SetTransferAmount(Entity<IvDripComponent> ent, FixedPoint2 amount, EntityUid? user = null)
    {
        if (ent.Comp.TransferAmount == amount || !ent.Comp.TransferAmounts.Contains(amount))
            return;

        ent.Comp.TransferAmount = amount;
        Dirty(ent);

        if (user != null)
        {
            _popup.PopupClient(
                Loc.GetString("is14-iv-drip-rate-set", ("amount", amount), ("seconds", ent.Comp.TransferInterval.TotalSeconds)),
                ent,
                user.Value);
        }
    }

    /// <summary>
    /// What is left in the bag, which way it is going and how fast. All three matter for
    /// the same decision — whether this patient can be left alone for another minute —
    /// and none of them are legible from the sprite at a glance.
    /// </summary>
    private void OnDripExamined(Entity<IvDripComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(IvDripComponent)))
        {
            args.PushMarkup(Loc.GetString(ent.Comp.Mode == IvDripMode.Draw
                ? "is14-iv-drip-examine-mode-draw"
                : "is14-iv-drip-examine-mode-inject"));

            args.PushMarkup(Loc.GetString(
                "is14-iv-drip-examine-rate",
                ("amount", ent.Comp.TransferAmount),
                ("seconds", ent.Comp.TransferInterval.TotalSeconds)));

            args.PushMarkup(GetPackSolution(ent) is { } solution
                ? Loc.GetString(
                    "is14-iv-drip-examine-pack",
                    ("volume", solution.Volume),
                    ("max", solution.MaxVolume))
                : Loc.GetString("is14-iv-drip-examine-no-pack"));

            // Whatever is written on the bag, repeated where the decision is made. It is the
            // label rather than the contents on purpose — the stand has no more idea what is
            // really in there than the doctor does.
            if (GetPack(ent) is { } pack
                && TryComp<BloodLabelComponent>(pack, out var label)
                && label.Type is { } group
                && _protos.TryIndex(group, out var proto))
            {
                args.PushMarkup(Loc.GetString(
                    "is14-iv-drip-examine-group",
                    ("type", Loc.GetString(proto.ShortName)),
                    ("color", (proto.Color ?? Color.White).ToHex())));
            }
        }
    }

    /// <summary>
    /// Swapping direction mid-transfusion is allowed on purpose. A doctor who realises
    /// they are pushing saline into somebody who needed blood taken out should be able to
    /// fix it from where they stand, not by pulling the needle and starting over.
    /// </summary>
    public void SetMode(Entity<IvDripComponent> ent, IvDripMode mode, EntityUid? user = null)
    {
        if (ent.Comp.Mode == mode)
            return;

        ent.Comp.Mode = mode;
        ent.Comp.Flowing = false;
        Dirty(ent);

        if (user != null)
        {
            _popup.PopupClient(
                Loc.GetString(mode == IvDripMode.Draw ? "is14-iv-drip-mode-draw" : "is14-iv-drip-mode-inject"),
                ent,
                user.Value);
        }

        UpdateAppearance(ent);
    }

    // ── Attach and detach ─────────────────────────────────────────────────────

    /// <summary>Puts the needle in and hangs the line between the stand and the patient.</summary>
    public bool Attach(Entity<IvDripComponent> ent, EntityUid patient)
    {
        if (ent.Comp.Patient != null
            || !HasComp<BloodstreamComponent>(patient)
            || HasComp<IvNeedleComponent>(patient)
            || TerminatingOrDeleted(patient))
        {
            return false;
        }

        // The needle component is the patient's half of this and is state the server owns:
        // predicting a component onto somebody else's mob buys nothing and can leave the
        // client showing an alert the server never granted.
        if (_net.IsClient)
            return true;

        ent.Comp.Patient = patient;
        ent.Comp.NextTransfer = default;
        Dirty(ent);

        var needle = EnsureComp<IvNeedleComponent>(patient);
        needle.Drip = ent.Owner;
        Dirty(patient, needle);

        _alerts.ShowAlert(patient, needle.Alert);
        _cord.Attach(ent.Owner, patient, ent.Comp.Range);
        _audio.PlayPvs(ent.Comp.AttachSound, patient);

        _popup.PopupEntity(
            Loc.GetString("is14-iv-drip-attached", ("target", Identity.Entity(patient, EntityManager))),
            patient);

        UpdateAppearance(ent);
        return true;
    }

    /// <summary>Pulls the needle, whoever pulled it and whatever the reason.</summary>
    public void Detach(Entity<IvDripComponent> ent)
    {
        if (ent.Comp.Patient is not { } patient || _net.IsClient)
            return;

        ent.Comp.Patient = null;
        ent.Comp.Flowing = false;
        Dirty(ent);

        _cord.Detach(ent.Owner);

        if (!TerminatingOrDeleted(patient))
        {
            if (TryComp<IvNeedleComponent>(patient, out var needle))
                _alerts.ClearAlert(patient, needle.Alert);

            RemCompDeferred<IvNeedleComponent>(patient);
            _audio.PlayPvs(ent.Comp.DetachSound, patient);

            _popup.PopupEntity(
                Loc.GetString("is14-iv-drip-detached", ("target", Identity.Entity(patient, EntityManager))),
                patient);
        }

        UpdateAppearance(ent);
    }

    /// <summary>
    /// Cleanup for the stand being destroyed with somebody still on it. Nothing reels the
    /// line in — the stand is gone, so the needle simply is not in anybody any more.
    /// </summary>
    private void OnDripShutdown(Entity<IvDripComponent> ent, ref ComponentShutdown args)
    {
        Detach(ent);
    }

    /// <summary>
    /// The patient's half going away — gibbed, deleted, or an admin taking the component
    /// off — has to reach the stand, or it keeps pumping into a corpse that no longer has
    /// the needle.
    /// </summary>
    private void OnNeedleShutdown(Entity<IvNeedleComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<IvDripComponent>(ent.Comp.Drip, out var drip) || drip.Patient != ent.Owner)
            return;

        Detach((ent.Comp.Drip, drip));
    }

    private void OnPackInserted(Entity<IvDripComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.SlotId)
            UpdateAppearance(ent);
    }

    private void OnPackRemoved(Entity<IvDripComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.SlotId)
            UpdateAppearance(ent);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>The bag on the stand, or null when nothing is hanging.</summary>
    public EntityUid? GetPack(Entity<IvDripComponent> ent)
    {
        return _container.TryGetContainer(ent.Owner, ent.Comp.SlotId, out var container)
               && container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0]
            : null;
    }

    /// <summary>What is in the bag on the stand, or null when there is no bag.</summary>
    public Solution? GetPackSolution(Entity<IvDripComponent> ent)
    {
        return GetPack(ent) is { } pack
               && TryComp<IvPackComponent>(pack, out var packComp)
               && _solutions.TryGetSolution(pack, packComp.SolutionName, out _, out var solution)
            ? solution
            : null;
    }

    /// <summary>Whether the patient has walked out from under the line.</summary>
    protected bool OutOfRange(Entity<IvDripComponent> ent, EntityUid patient)
    {
        var dripPos = _transform.GetMapCoordinates(ent.Owner);
        var patientPos = _transform.GetMapCoordinates(patient);

        return dripPos.MapId != patientPos.MapId
               || (dripPos.Position - patientPos.Position).Length() > ent.Comp.Range;
    }

    private TimeSpan GetDetachDelay(Entity<IvNeedleComponent> ent)
    {
        return TryComp<IvDripComponent>(ent.Comp.Drip, out var drip)
            ? drip.DetachDelay
            : TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Repaints the stand and the line. Only the server has any business doing this — it
    /// is all derived from the bag's contents — so the shared half does nothing.
    /// </summary>
    protected virtual void UpdateAppearance(Entity<IvDripComponent> ent)
    {
    }
}

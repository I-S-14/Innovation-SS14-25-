// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Popups;
using Content.Shared._IS14.Cord;
using Content.Shared._IS14.Leash;
using Content.Shared.Containers;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared.Wieldable.Components;
using Content.Shared.Wieldable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Leash;

/// <summary>
/// Tools on a short lead: taking them off their anchor, putting them back, and dragging
/// them home when somebody walks too far or lets go.
///
/// Nothing here knows what the tool does. Systems that care subscribe to
/// <see cref="LeashTakenEvent"/> and <see cref="LeashReturnedEvent"/> on the anchor.
/// </summary>
public sealed class LeashSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedCordSystem _cord = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedWieldableSystem _wieldable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// How often the lead is measured. Tools leave by being walked away with, which is
    /// slow — this only has to fire often enough that the lead snaps while the anchor is
    /// still on screen.
    /// </summary>
    private static readonly TimeSpan RangeCheckInterval = TimeSpan.FromSeconds(0.4);

    private TimeSpan _nextRangeCheck;

    /// <summary>
    /// Set while a tool is being stowed. Pulling it out of a hand raises a drop event of
    /// its own, and without this the two would call each other.
    /// </summary>
    private bool _stowing;

    public override void Initialize()
    {
        base.Initialize();

        // The tool is put in the cradle by ContainerFill, so there is nothing to bind to
        // until that has run.
        SubscribeLocalEvent<LeashAnchorComponent, MapInitEvent>(OnAnchorMapInit,
            after: new[] { typeof(ContainerFillSystem) });

        SubscribeLocalEvent<LeashAnchorComponent, InteractUsingEvent>(OnAnchorInteractUsing);
        SubscribeLocalEvent<LeashAnchorComponent, GetVerbsEvent<AlternativeVerb>>(OnAnchorAltVerbs);

        SubscribeLocalEvent<LeashedItemComponent, AfterInteractEvent>(OnItemAfterInteract);
        SubscribeLocalEvent<LeashedItemComponent, BeforeThrowEvent>(OnItemBeforeThrow);
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ties the anchor to whatever the prototype put in its cradle. Doing it here rather
    /// than in YAML means a mapper cannot produce an anchor whose tool belongs to
    /// nothing, which would be a tool that silently refuses to work.
    /// </summary>
    private void OnAnchorMapInit(EntityUid uid, LeashAnchorComponent component, MapInitEvent args)
    {
        var cradle = _container.EnsureContainer<ContainerSlot>(uid, component.SlotId);

        if (cradle.ContainedEntity is not { } item)
            return;

        component.Leashed = item;
        Dirty(uid, component);

        var leashed = EnsureComp<LeashedItemComponent>(item);
        leashed.Anchor = uid;
        Dirty(item, leashed);
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    /// <summary>Clicking the anchor with its own tool stows it again.</summary>
    private void OnAnchorInteractUsing(EntityUid uid, LeashAnchorComponent component, InteractUsingEvent args)
    {
        if (args.Handled || args.Used != component.Leashed)
            return;

        args.Handled = TryStow((uid, component));
    }

    private void OnAnchorAltVerbs(EntityUid uid, LeashAnchorComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || component.Leashed == null)
            return;

        var ent = (uid, component);
        var user = args.User;
        var stowed = IsStowed(ent);

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(stowed ? component.TakeVerb : component.StowVerb),
            Act = () =>
            {
                if (stowed)
                    TryTake(ent, user);
                else
                    TryStow(ent);
            },
        });
    }

    /// <summary>
    /// Touching your own anchor with the tool puts it away. The rack the anchor is
    /// sitting in counts as the anchor — nobody means to use a fuel nozzle on the pump
    /// it came out of.
    /// </summary>
    private void OnItemAfterInteract(EntityUid uid, LeashedItemComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (component.Anchor is not { } anchor || !TryComp<LeashAnchorComponent>(anchor, out var anchorComp))
            return;

        if (target != anchor && Transform(anchor).ParentUid != target)
            return;

        args.Handled = TryStow((anchor, anchorComp));
    }

    /// <summary>
    /// Throwing a leashed tool reels it in instead. It is on a lead — there is nowhere
    /// for it to fly to, and letting it arc across the room and land by the anchor looks
    /// like a bug even when the end state is right.
    /// </summary>
    private void OnItemBeforeThrow(EntityUid uid, LeashedItemComponent component, ref BeforeThrowEvent args)
    {
        if (component.Anchor is not { } anchor || !TryComp<LeashAnchorComponent>(anchor, out var anchorComp))
            return;

        if (!anchorComp.ReturnOnRelease)
            return;

        args.Cancelled = true;
        TryStow((anchor, anchorComp));
    }

    // ── The lead ──────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Range is measured a few times a second; whether the tool is still in a hand is
        // checked every tick, because that one has to look instant.
        var checkRange = _timing.CurTime >= _nextRangeCheck;
        if (checkRange)
            _nextRangeCheck = _timing.CurTime + RangeCheckInterval;

        var query = EntityQueryEnumerator<LeashAnchorComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            Entity<LeashAnchorComponent> ent = (uid, component);

            if (component.Leashed is not { } item || !Exists(item) || IsStowed(ent))
                continue;

            // A tool nobody is holding reels straight in. Checked here rather than on the
            // drop event because a drop finishes by teleporting the item to where the
            // player clicked — stowing it mid-drop just gets undone.
            if (component.ReturnOnRelease && !IsHeld(item))
            {
                TryStow(ent);
                continue;
            }

            if (!checkRange || WithinRange(ent, item))
                continue;

            Recall(ent, item);
        }
    }

    /// <summary>
    /// Measures the lead in map terms rather than through the transform hierarchy: the
    /// tool is in somebody's hands and the anchor may itself be racked in something, so
    /// neither end is a plain entity sitting on the grid.
    /// </summary>
    private bool WithinRange(Entity<LeashAnchorComponent> ent, EntityUid item)
    {
        var anchorPos = _transform.GetMapCoordinates(ent.Owner);
        var itemPos = _transform.GetMapCoordinates(item);

        if (anchorPos.MapId != itemPos.MapId)
            return false;

        return (anchorPos.Position - itemPos.Position).Length() <= ent.Comp.Range;
    }

    /// <summary>Drags the tool home out of whoever's hands it was in.</summary>
    private void Recall(Entity<LeashAnchorComponent> ent, EntityUid item)
    {
        // Tell the person holding it why their hands just emptied. A tool lying on the
        // floor has nobody to tell, and the sound covers that case.
        if (_container.TryGetContainingContainer((item, null, null), out var held))
            _popup.PopupEntity(Loc.GetString(ent.Comp.RecallPopup), item, held.Owner);

        Stow(ent, item, recalled: true);

        _audio.PlayPvs(ent.Comp.RecallSound, ent.Owner);
    }

    // ── Take and stow ─────────────────────────────────────────────────────────

    /// <summary>Pulls the tool out of the cradle and into a free hand.</summary>
    public bool TryTake(Entity<LeashAnchorComponent> ent, EntityUid user)
    {
        if (ent.Comp.Leashed is not { } item || !Exists(item))
            return false;

        if (!IsStowed(ent))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.AlreadyOutPopup), ent.Owner, user);
            return false;
        }

        if (!_container.TryGetContainer(ent.Owner, ent.Comp.SlotId, out var cradle))
            return false;

        // Where the tool flies from, taken before it moves: the anchor it came off,
        // which is right even when the anchor is itself racked in something else.
        var origin = _transform.GetMapCoordinates(ent.Owner);

        _container.Remove(item, cradle);

        // Animated below by hand, so the built-in one is skipped: it deliberately leaves
        // out the person doing the picking up, on the grounds that their own client has
        // already predicted it. None of this is predicted, so they would be the one
        // person who saw the tool simply appear.
        if (!_hands.TryPickupAnyHand(user, item, animate: false))
        {
            // No free hand: the tool goes straight back rather than onto the floor, which
            // would leave a lead hanging off an anchor nobody is working with.
            _container.Insert(item, cradle);
            _popup.PopupEntity(Loc.GetString(ent.Comp.NoHandPopup), ent.Owner, user);
            return false;
        }

        AnimateFlight(item, origin, user);
        _cord.Attach(item, ent.Owner, ent.Comp.Range);

        var ev = new LeashTakenEvent(item, user);
        RaiseLocalEvent(ent.Owner, ref ev);

        return true;
    }

    /// <summary>Puts the tool back on its anchor.</summary>
    public bool TryStow(Entity<LeashAnchorComponent> ent)
    {
        if (ent.Comp.Leashed is not { } item || !Exists(item) || IsStowed(ent))
            return false;

        return Stow(ent, item, recalled: false);
    }

    private bool Stow(Entity<LeashAnchorComponent> ent, EntityUid item, bool recalled)
    {
        if (_stowing || !_container.TryGetContainer(ent.Owner, ent.Comp.SlotId, out var cradle))
            return false;

        ForceUnwield(item);

        // Taken before the insert moves it: wherever the tool was is where the lead
        // reels it in from — a hand, the floor, or halfway across the room.
        var origin = _transform.GetMapCoordinates(item);

        _cord.Detach(item);

        _stowing = true;
        var inserted = _container.Insert(item, cradle);
        _stowing = false;

        if (!inserted)
            return false;

        AnimateFlight(item, origin, ent.Owner);

        var ev = new LeashReturnedEvent(item, recalled);
        RaiseLocalEvent(ent.Owner, ref ev);

        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Whether the tool is sitting in its cradle right now.</summary>
    public bool IsStowed(Entity<LeashAnchorComponent> ent)
    {
        return ent.Comp.Leashed is { } item
               && _container.TryGetContainer(ent.Owner, ent.Comp.SlotId, out var cradle)
               && cradle.Contains(item);
    }

    /// <summary>
    /// Whether somebody actually has the tool in a hand. A bag or a locker does not
    /// count — a lead has to end somewhere a person is, or it ends at the anchor.
    /// </summary>
    private bool IsHeld(EntityUid item)
    {
        return _container.TryGetContainingContainer((item, null, null), out var container)
               && TryComp<HandsComponent>(container.Owner, out var hands)
               && _hands.IsHolding((container.Owner, hands), item, out _);
    }

    private void ForceUnwield(EntityUid item)
    {
        if (!TryComp<WieldableComponent>(item, out var wieldable) || !wieldable.Wielded)
            return;

        if (_container.TryGetContainingContainer((item, null, null), out var held))
            _wieldable.TryUnwield(item, wieldable, held.Owner, force: true);
    }

    /// <summary>
    /// Flies the tool's sprite from one place to another — off the anchor into a hand,
    /// or back off the floor into the cradle. Without it the tool teleports, which reads
    /// as a bug even when the end state is right.
    /// </summary>
    /// <remarks>
    /// The user is deliberately left out of the call, so the animation reaches everyone
    /// in PVS including whoever caused it. The exclusion built into the engine's version
    /// assumes the actor's own client predicted the move; none of this is predicted.
    /// </remarks>
    private void AnimateFlight(EntityUid item, MapCoordinates from, EntityUid to)
    {
        var toXform = Transform(to);

        if (from.MapId != toXform.MapID)
            return;

        var anchor = toXform.ParentUid.IsValid() ? toXform.ParentUid : to;

        _storage.PlayPickupAnimation(item, _transform.ToCoordinates(anchor, from), toXform.Coordinates, Angle.Zero);
    }
}

// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._IS14.Leash;
using Content.Server.Medical;
using Content.Server.Popups;
using Content.Shared._IS14.Cord;
using Content.Shared._IS14.Leash;
using Content.Shared._IS14.Medical.Defibrillator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Medical;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.Defibrillator;

/// <summary>
/// What is actually specific to a defibrillator: the cell behind a screwdriver, the
/// charge gauge, and turning a pair of wielded paddles into a shock.
///
/// Everything the paddles being on a cord implies — taking them, stowing them, the
/// visible cable, being dragged back when the lead runs out — is the generic leash and
/// cord, and nothing in this file has to know about it. The medicine is upstream's
/// <see cref="DefibrillatorSystem"/>: this only decides when to ask for it.
/// </summary>
public sealed class DefibUnitSystem : EntitySystem
{
    [Dependency] private readonly DefibrillatorSystem _defibrillator = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedCordSystem _cord = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedWieldableSystem _wieldable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>How often the charge gauge is re-read. It only moves while charging or in use.</summary>
    private static readonly TimeSpan GaugeInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextGaugeUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefibUnitComponent, MapInitEvent>(OnUnitMapInit);
        SubscribeLocalEvent<DefibUnitComponent, InteractUsingEvent>(OnUnitInteractUsing);
        SubscribeLocalEvent<DefibUnitComponent, ItemToggledEvent>(OnUnitToggled);
        SubscribeLocalEvent<DefibUnitComponent, PowerCellChangedEvent>(OnUnitCellChanged);
        SubscribeLocalEvent<DefibUnitComponent, DefibCellDoAfterEvent>(OnCellDoAfter);
        SubscribeLocalEvent<DefibUnitComponent, LeashTakenEvent>(OnPaddlesTaken);
        SubscribeLocalEvent<DefibUnitComponent, LeashReturnedEvent>(OnPaddlesReturned);

        // After the leash, so that touching the unit or its rack stows the paddles
        // instead of being read as an attempt to defibrillate a defibrillator.
        SubscribeLocalEvent<DefibPaddlesComponent, AfterInteractEvent>(OnPaddlesAfterInteract,
            after: new[] { typeof(LeashSystem) });
        SubscribeLocalEvent<DefibPaddlesComponent, DefibPaddlesZapDoAfterEvent>(OnPaddlesZapDoAfter);
        SubscribeLocalEvent<DefibPaddlesComponent, ItemWieldedEvent>(OnPaddlesWielded);
        SubscribeLocalEvent<DefibPaddlesComponent, ItemUnwieldedEvent>(OnPaddlesUnwielded);
        SubscribeLocalEvent<DefibPaddlesComponent, WieldAttemptEvent>(OnPaddlesWieldAttempt);
    }

    // ── Unit ──────────────────────────────────────────────────────────────────

    private void OnUnitMapInit(EntityUid uid, DefibUnitComponent component, MapInitEvent args)
    {
        UpdateAppearance((uid, component));
    }

    /// <summary>
    /// The cell comes out with a screwdriver and nothing else. A slot that pops open on
    /// click would steal every click meant for the unit, and swapping a cell is
    /// maintenance work — not something to do with a patient on the floor.
    /// </summary>
    private void OnUnitInteractUsing(EntityUid uid, DefibUnitComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, component.CellTool))
            return;

        args.Handled = _tool.UseTool(
            args.Used,
            args.User,
            uid,
            component.CellToolDelay,
            component.CellTool,
            new DefibCellDoAfterEvent());
    }

    /// <summary>Unscrewing finished — hand the cell over.</summary>
    private void OnCellDoAfter(EntityUid uid, DefibUnitComponent component, DefibCellDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<PowerCellSlotComponent>(uid, out var slot)
            || !_itemSlots.TryGetSlot(uid, slot.CellSlotId, out var cellSlot))
        {
            return;
        }

        if (cellSlot.Item == null)
        {
            _popup.PopupEntity(Loc.GetString("is14-defib-unit-no-cell"), uid, args.User);
            return;
        }

        args.Handled = _itemSlots.TryEjectToHands(uid, cellSlot, args.User, excludeUserAudio: true);
        UpdateAppearance((uid, component));
    }

    /// <summary>
    /// The unit can switch itself off — upstream does exactly that when the cell runs
    /// too low for another shot. Paddles left wielded on a dead unit would be a lie, so
    /// they come down with it, and the cord stops glowing.
    /// </summary>
    private void OnUnitToggled(EntityUid uid, DefibUnitComponent component, ref ItemToggledEvent args)
    {
        if (TryComp<LeashAnchorComponent>(uid, out var leash) && leash.Leashed is { } paddles)
        {
            if (!args.Activated)
                ForceUnwield(paddles);

            _cord.SetEnergized(paddles, args.Activated);
        }

        UpdateAppearance((uid, component));
    }

    private void OnUnitCellChanged(EntityUid uid, DefibUnitComponent component, ref PowerCellChangedEvent args)
    {
        UpdateAppearance((uid, component));
    }

    /// <summary>A cord that is already live when it is unreeled has to show it straight away.</summary>
    private void OnPaddlesTaken(EntityUid uid, DefibUnitComponent component, ref LeashTakenEvent args)
    {
        _cord.SetEnergized(args.Item, _toggle.IsActivated(uid));
        UpdateAppearance((uid, component));
    }

    /// <summary>Stowed paddles cannot be holding a charge, whoever put them back.</summary>
    private void OnPaddlesReturned(EntityUid uid, DefibUnitComponent component, ref LeashReturnedEvent args)
    {
        _toggle.TryDeactivate(uid);
        UpdateAppearance((uid, component));
    }

    // ── Paddles ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Everything the paddles touch is handed to the unit. The user still has to be
    /// holding them in both hands: one-handed paddles are just a lump of plastic.
    /// </summary>
    private void OnPaddlesAfterInteract(EntityUid uid, DefibPaddlesComponent component, AfterInteractEvent args)
    {
        // Touching the unit or its rack is the leash's business — it stows the paddles
        // and marks the interaction handled before this ever runs.
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (GetUnit(uid) is not { } unit)
            return;

        if (!IsWielded(uid))
        {
            _popup.PopupEntity(Loc.GetString("is14-defib-paddles-not-wielded"), uid, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = TryStartZap(uid, unit, target, args.User);
    }

    /// <summary>
    /// Starts the charge-up. This deliberately does not use upstream's TryStartZap: that
    /// one hangs the do-after off the defibrillator itself, and the do-after's reach
    /// checks then measure the box rather than the doctor. With paddles on a cord the box
    /// is regularly round a corner, which would fail a shock the doctor is standing right
    /// over. The paddles are what is in their hand, so the paddles are the tool.
    /// </summary>
    private bool TryStartZap(EntityUid paddles, EntityUid unit, EntityUid target, EntityUid user)
    {
        if (!TryComp<DefibrillatorComponent>(unit, out var defib))
            return false;

        if (!_defibrillator.CanZap(unit, target, user, defib))
            return false;

        // The charge-up whine still comes from the box, because that is the thing
        // actually charging.
        _audio.PlayPvs(defib.ChargeSound, unit);

        // The stock 1.5m leash on a do-after is meant for a defibrillator you carry, and
        // it kills the shock the moment the doctor shifts a tile off the patient. The
        // cord is already the leash here — and unlike a hidden threshold, the player can
        // watch it straighten — so the do-after is given exactly the cord's reach. The
        // zap now dies at the same moment the cord does, and never before.
        var range = TryComp<LeashAnchorComponent>(unit, out var leash) ? leash.Range : 1.5f;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            defib.DoAfterDuration,
            new DefibPaddlesZapDoAfterEvent(),
            paddles,
            target,
            paddles)
        {
            NeedHand = true,
            BreakOnMove = !defib.AllowDoAfterMovement,
            DistanceThreshold = range,
            MultiplyDelay = false,
        });
    }

    /// <summary>Charge-up finished — hand the shock itself back to upstream.</summary>
    private void OnPaddlesZapDoAfter(EntityUid uid, DefibPaddlesComponent component, DefibPaddlesZapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (GetUnit(uid) is not { } unit)
            return;

        if (!_defibrillator.CanZap(unit, target, args.User))
            return;

        args.Handled = true;
        _defibrillator.Zap(unit, target, args.User);
    }

    /// <summary>Wielding is the switch: both hands on the paddles charges the unit up.</summary>
    private void OnPaddlesWielded(EntityUid uid, DefibPaddlesComponent component, ref ItemWieldedEvent args)
    {
        if (GetUnit(uid) is not { } unit || !TryComp<DefibUnitComponent>(unit, out var unitComp))
            return;

        if (!_toggle.TryActivate(unit, args.User))
            return;

        // The tone plays at the paddles, not at the box: the paddles are what the user
        // just did something with, and the box may be three tiles behind them.
        _audio.PlayPvs(unitComp.WieldSound, uid);
    }

    private void OnPaddlesUnwielded(EntityUid uid, DefibPaddlesComponent component, ref ItemUnwieldedEvent args)
    {
        if (GetUnit(uid) is not { } unit || !TryComp<DefibUnitComponent>(unit, out var unitComp))
            return;

        if (!_toggle.TryDeactivate(unit, args.User))
            return;

        _audio.PlayPvs(unitComp.UnwieldSound, uid);
    }

    /// <summary>
    /// Refuses the wield rather than letting the unit fail the zap later: a doctor who
    /// squeezes the paddles and gets no charge tone should learn that from the paddles,
    /// not from a corpse that stays dead.
    /// </summary>
    private void OnPaddlesWieldAttempt(EntityUid uid, DefibPaddlesComponent component, ref WieldAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (GetUnit(uid) is not { } unit)
        {
            args.Message = Loc.GetString("is14-defib-paddles-no-unit");
            args.Cancel();
            return;
        }

        if (!_powerCell.HasBattery(unit))
        {
            args.Message = Loc.GetString("is14-defib-unit-no-cell");
            args.Cancel();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>The box these paddles are on the end of, if they are on one at all.</summary>
    private EntityUid? GetUnit(EntityUid paddles)
    {
        if (!TryComp<LeashedItemComponent>(paddles, out var leashed) || leashed.Anchor is not { } anchor)
            return null;

        return HasComp<DefibUnitComponent>(anchor) ? anchor : null;
    }

    private bool IsWielded(EntityUid paddles)
    {
        return TryComp<WieldableComponent>(paddles, out var wieldable) && wieldable.Wielded;
    }

    private void ForceUnwield(EntityUid paddles)
    {
        if (!TryComp<WieldableComponent>(paddles, out var wieldable) || !wieldable.Wielded)
            return;

        if (_container.TryGetContainingContainer((paddles, null, null), out var held))
            _wieldable.TryUnwield(paddles, wieldable, held.Owner, force: true);
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextGaugeUpdate)
            return;

        _nextGaugeUpdate = _timing.CurTime + GaugeInterval;

        // The gauge moves without anyone touching the unit — draining while it is charged
        // up, filling while it sits in a wall station — so the sprite is refreshed here
        // rather than only on events, and only when a step is actually crossed.
        var query = EntityQueryEnumerator<DefibUnitComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (GetChargeLevel((uid, component)) != component.LastChargeLevel)
                UpdateAppearance((uid, component));
        }
    }

    /// <summary>
    /// Pushes the unit's state onto its sprite. Charge is bucketed here so the client
    /// never has to decide what counts as a quarter full.
    /// </summary>
    public void UpdateAppearance(Entity<DefibUnitComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        var level = GetChargeLevel(ent);
        ent.Comp.LastChargeLevel = level;

        var stowed = !TryComp<LeashAnchorComponent>(ent, out var leash)
                     || leash.Leashed is not { } paddles
                     || Transform(paddles).ParentUid == ent.Owner;

        _appearance.SetData(ent, DefibUnitVisuals.HasCell, _powerCell.HasBattery(ent.Owner), appearance);
        _appearance.SetData(ent, DefibUnitVisuals.Charge, level, appearance);
        _appearance.SetData(ent, DefibUnitVisuals.PaddlesDocked, stowed, appearance);
        _appearance.SetData(ent, DefibUnitVisuals.Active, _toggle.IsActivated(ent.Owner), appearance);
    }

    /// <summary>Charge bucket of the unit, also relayed to whatever rack it sits in.</summary>
    public int GetChargeLevel(Entity<DefibUnitComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0;

        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            return 0;

        return DefibChargeLevels.FromFraction(_battery.GetChargeLevel(battery.Value.AsNullable()));
    }
}

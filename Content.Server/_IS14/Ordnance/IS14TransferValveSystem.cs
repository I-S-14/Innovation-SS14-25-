// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._IS14.Ordnance;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Content.Shared.Interaction.Events;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server._IS14.Ordnance;

/// <summary>
/// Runs the tank transfer valve: an unwrenched manual valve with two tanks on it. Opening it dumps
/// one tank into the other and burns whatever the mixture can burn.
/// </summary>
/// <remarks>
/// The yield is computed here rather than left to the tank's own burst rules, because those can
/// never fire: a canister fills a tank to ten atmospheres at most, so merging two of them lands at
/// twenty — half of what it takes to even rupture one — and a plasma fire inside five litres is
/// divided by the atmos heat scale and crawls. So the valve does what the tile fire would have
/// done, all at once: it burns the fuel the oxidiser can reach and turns that energy into a blast.
/// Everything interesting therefore lives in the mixture — how hot it is and what is in it — which
/// is the point of the whole department: a bomb is a chemistry problem, not a crafting recipe.
/// </remarks>
public sealed class IS14TransferValveSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Off the pipe it is an ordnance assembly; in the pipe it is a pipe valve and nothing else.
        SubscribeLocalEvent<IS14TransferValveComponent, AnchorStateChangedEvent>(OnAnchorChanged);

        SubscribeLocalEvent<IS14TransferValveComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<IS14TransferValveComponent, EntInsertedIntoContainerMessage>(OnContentsChanged);
        SubscribeLocalEvent<IS14TransferValveComponent, EntRemovedFromContainerMessage>(OnContentsChanged);

        SubscribeLocalEvent<IS14TransferValveComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<IS14TransferValveComponent, ActivateInWorldEvent>(OnActivate);

        SubscribeLocalEvent<IS14TransferValveComponent, AttemptTriggerEvent>(OnAttemptTrigger);
        SubscribeLocalEvent<IS14TransferValveComponent, TriggerEvent>(OnTrigger);
    }

    private void OnAnchorChanged(Entity<IS14TransferValveComponent> ent, ref AnchorStateChangedEvent args)
    {
        // Wrenching a loaded valve back into a pipe hands the parts back instead of hiding two
        // tanks' worth of gas inside a pipe fitting.
        if (args.Anchored)
            EjectParts(ent);
    }

    private void OnContentsChanged<T>(Entity<IS14TransferValveComponent> ent, ref T args)
        where T : ContainerModifiedMessage
    {
        // Spent is a property of the pair of tanks, not of the valve: changing what is on it makes
        // it a working valve again. Without this, one merge bricked the valve for good — it went on
        // refusing tanks with no way to tell why.
        if (ent.Comp.Open)
        {
            ent.Comp.Open = false;
            Dirty(ent);
        }

        UpdateTimer(ent);
        UpdateAppearance(ent);
    }

    /// <summary>
    /// The countdown lives and dies with the fitted timer: no timer, no timing, and the delay verbs
    /// are only ever on a valve that actually has one.
    /// </summary>
    private void UpdateTimer(Entity<IS14TransferValveComponent> ent)
    {
        // Deleting an entity empties its containers on the way out, and one that is already
        // terminating will not take a new component.
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (_slots.GetItemOrNull(ent.Owner, ent.Comp.TriggerSlot) is not { } part
            || !TryComp<IS14OrdnanceTimerComponent>(part, out var fitted))
        {
            RemComp<TimerTriggerComponent>(ent.Owner);
            return;
        }

        var timer = EnsureComp<TimerTriggerComponent>(ent.Owner);

        timer.Delay = fitted.Delay;
        timer.DelayOptions = fitted.DelayOptions;
        timer.BeepSound = null;
        timer.Examinable = false;
        timer.Popup = null;

        Dirty(ent.Owner, timer);
    }

    private void UpdateAppearance(Entity<IS14TransferValveComponent> ent)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        var tanks = 0;

        if (_slots.GetItemOrNull(ent.Owner, ent.Comp.ReceiverSlot) != null)
            tanks++;

        if (_slots.GetItemOrNull(ent.Owner, ent.Comp.DonorSlot) != null)
            tanks++;

        _appearance.SetData(ent.Owner, IS14TransferValveVisuals.Tanks, tanks);
        _appearance.SetData(ent.Owner, IS14TransferValveVisuals.Trigger,
            _slots.GetItemOrNull(ent.Owner, ent.Comp.TriggerSlot) != null);
    }

    private void OnInsertAttempt(Entity<IS14TransferValveComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (Transform(ent.Owner).Anchored || ent.Comp.Open)
            args.Cancelled = true;
    }

    private void OnUseInHand(Entity<IS14TransferValveComponent> ent, ref UseInHandEvent args)
    {
        args.Handled |= Start(ent, args.User);
    }

    private void OnActivate(Entity<IS14TransferValveComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!args.Complex)
            return;

        // Deliberately not checking Handled: the valve's own toggle runs as well, which is what
        // makes the sprite show it as open.
        Start(ent, args.User);
    }

    /// <summary>
    /// Opening the valve. With a timer fitted that means starting its countdown, and the valve opens
    /// when the countdown runs out.
    /// </summary>
    private bool Start(Entity<IS14TransferValveComponent> ent, EntityUid user)
    {
        if (!Ready(ent))
            return false;

        if (TryComp<TimerTriggerComponent>(ent.Owner, out var timer))
            return _trigger.ActivateTimerTrigger((ent.Owner, timer), user);

        return TryOpen(ent);
    }

    private void OnAttemptTrigger(Entity<IS14TransferValveComponent> ent, ref AttemptTriggerEvent args)
    {
        // Covers the countdown as well as the moment it ends: wrench the valve back into a pipe, or
        // pull a tank off it, and the timer stops mattering.
        if (!Ready(ent))
            args.Cancelled = true;
    }

    private void OnTrigger(Entity<IS14TransferValveComponent> ent, ref TriggerEvent args)
    {
        // The same key check every trigger effect does, so the valve answers the timer only.
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        if (TryOpen(ent))
            args.Handled = true;
    }

    /// <summary>Two tanks, off the pipe, not already spent.</summary>
    private bool Ready(Entity<IS14TransferValveComponent> ent)
    {
        return !ent.Comp.Open
               && !Transform(ent.Owner).Anchored
               && _slots.GetItemOrNull(ent.Owner, ent.Comp.ReceiverSlot) != null
               && _slots.GetItemOrNull(ent.Owner, ent.Comp.DonorSlot) != null;
    }

    private void EjectParts(Entity<IS14TransferValveComponent> ent)
    {
        foreach (var slotId in new[] { ent.Comp.ReceiverSlot, ent.Comp.DonorSlot, ent.Comp.TriggerSlot })
        {
            if (_slots.TryGetSlot(ent.Owner, slotId, out var slot))
                _slots.TryEject(ent.Owner, slot, null, out _);
        }
    }

    /// <summary>
    /// Opens the valve: everything in the donor tank goes into the receiver, which is then holding
    /// two tanks' worth of gas in one tank's volume, and burns.
    /// </summary>
    public bool TryOpen(Entity<IS14TransferValveComponent> ent)
    {
        if (!Ready(ent))
            return false;

        if (_slots.GetItemOrNull(ent.Owner, ent.Comp.ReceiverSlot) is not { } receiver
            || _slots.GetItemOrNull(ent.Owner, ent.Comp.DonorSlot) is not { } donor)
        {
            return false;
        }

        if (!TryComp<GasTankComponent>(receiver, out var receiverTank)
            || !TryComp<GasTankComponent>(donor, out var donorTank))
        {
            return false;
        }

        _atmos.Merge(receiverTank.Air, donorTank.Air);
        donorTank.Air.Clear();

        ent.Comp.Open = true;
        Dirty(ent);

        _audio.PlayPvs(ent.Comp.OpenSound, ent.Owner);

        var (_, intensity) = GetYield(ent.Comp, receiverTank.Air);

        // A mix with nothing to burn just equalises, and that is the whole answer: no bang, two
        // tanks of warm gas, work out why.
        if (intensity <= 0f)
            return true;

        _explosion.QueueExplosion(_transform.GetMapCoordinates(ent.Owner),
            ent.Comp.ExplosionType,
            intensity,
            ent.Comp.IntensitySlope,
            ent.Comp.MaxTileIntensity,
            ent.Owner);

        // The assembly is consumed by its own blast, tanks and all.
        QueueDel(ent.Owner);

        return true;
    }

    /// <summary>
    /// What the mixture is worth: the energy its oxidiser can actually release, and the explosion
    /// intensity that buys.
    /// </summary>
    /// <remarks>
    /// The same three things the tile fire cares about. Oxygen is the limit — one and a bit moles
    /// of it per mole of fuel — tritium burns far hotter than plasma, and a cold mix does not burn
    /// at all, with everything between the plasma ignition point and its upper temperature burning
    /// proportionally better. Hotter gas is therefore worth more even though a hot tank holds fewer
    /// moles, which is exactly the trade the ordnance crew is there to work out.
    /// </remarks>
    public (float Energy, float Intensity) GetYield(IS14TransferValveComponent comp, GasMixture air)
    {
        var oxygen = air.GetMoles(Gas.Oxygen);
        var plasma = air.GetMoles(Gas.Plasma);
        var tritium = air.GetMoles(Gas.Tritium);

        var fuel = plasma + tritium;

        if (fuel <= 0f || oxygen <= 0f)
            return (0f, 0f);

        var burnt = MathF.Min(fuel, oxygen / Atmospherics.OxygenBurnRateBase);

        var completeness = Math.Clamp(
            (air.Temperature - Atmospherics.PlasmaMinimumBurnTemperature)
            / (Atmospherics.PlasmaUpperTemperature - Atmospherics.PlasmaMinimumBurnTemperature),
            0f,
            1f);

        if (completeness <= 0f)
            return (0f, 0f);

        var energyPerMole = MathHelper.Lerp(Atmospherics.FirePlasmaEnergyReleased,
            Atmospherics.FireHydrogenEnergyReleased,
            tritium / fuel);

        var energy = burnt * energyPerMole * completeness;
        var intensity = MathF.Min(energy / comp.EnergyPerIntensity, comp.MaxIntensity);

        return (energy, intensity);
    }
}

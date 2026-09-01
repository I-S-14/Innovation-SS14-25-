// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RCD.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Bills the chassis for what its extended hardware actually does.
///
///     A module that hands out a tool cannot charge on activation alone: the drill is out
///     for as long as you want it out, and what costs power is swinging it. So the device
///     itself is metered — one <see cref="ChassisModuleComponent.UseCost"/> per swing, per
///     zap, per placed girder — and a suit that cannot pay simply does not fire.
/// </summary>
public sealed class ChassisDevicePowerSystem : EntitySystem
{
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChassisDeviceComponent, AttemptMeleeEvent>(OnMeleeAttempt);

        // Ahead of the RCD so an unaffordable placement never starts; the defibrillator
        // rides the same event and is charged per attempt to zap.
        SubscribeLocalEvent<ChassisDeviceComponent, AfterInteractEvent>(
            OnAfterInteract,
            before: [typeof(RCDSystem)]);
    }

    private void OnMeleeAttempt(Entity<ChassisDeviceComponent> ent, ref AttemptMeleeEvent args)
    {
        if (args.Cancelled || TrySpend(ent, args.User))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("chassis-device-no-power");
    }

    private void OnAfterInteract(Entity<ChassisDeviceComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || TrySpend(ent, args.User))
            return;

        // Handled, so whoever would have acted on this click stands down.
        args.Handled = true;
        _popup.PopupClient(Loc.GetString("chassis-device-no-power"), ent, args.User);
    }

    /// <summary>
    ///     Takes one use out of the chassis. True when the device may go ahead — which
    ///     includes devices that cost nothing at all.
    /// </summary>
    private bool TrySpend(Entity<ChassisDeviceComponent> ent, EntityUid user)
    {
        if (ent.Comp.Module is not { } module
            || !TryComp<ChassisModuleComponent>(module, out var comp)
            || comp.Chassis is not { } chassis)
            return true;

        if (comp.UseCost <= 0f)
            return true;

        return _power.TryUseCharge(chassis, comp.UseCost);
    }
}

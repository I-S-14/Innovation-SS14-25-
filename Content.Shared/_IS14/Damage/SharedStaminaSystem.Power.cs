using Content.Shared._Shitcode.Weapons.Misc;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Damage.Systems;

public abstract partial class SharedStaminaSystem
{
    [Dependency] private readonly SharedBatterySystem _compatStaminaBattery = default!;
    [Dependency] private readonly ItemToggleSystem _compatStaminaToggle = default!;

    [SubscribeLocalEvent]
    private void OnPoweredStaminaAttempt(Entity<StaminaDamageOnHitRequiresChargeComponent> ent,
        ref StaminaDamageOnHitAttemptEvent args)
    {
        if (!args.Cancelled && !HasComp<StunbatonComponent>(ent) &&
            _compatStaminaBattery.GetCharge(ent.Owner).Charge < ent.Comp.RequiredCharge)
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnToggledStaminaAttempt(Entity<StaminaDamageOnHitRequiresToggleComponent> ent,
        ref StaminaDamageOnHitAttemptEvent args)
    {
        if (!args.Cancelled && !_compatStaminaToggle.IsActivated(ent.Owner))
            args.Cancelled = true;
    }

    private void InitializePowerCompat()
    {
        SubscribeLocalEvent<StaminaDamageOnHitRequiresChargeComponent, StaminaDamageMeleeHitEvent>(OnPoweredStaminaHit);
    }

    private void OnPoweredStaminaHit(EntityUid uid, StaminaDamageOnHitRequiresChargeComponent component,
        StaminaDamageMeleeHitEvent args)
    {
        // Legacy Goob batons consume their configured energy in their own attempt handler.
        if (args.HitEntities.Count > 0 && !HasComp<StunbatonComponent>(uid))
            _compatStaminaBattery.TryUseCharge(uid, component.RequiredCharge);
    }

    [SubscribeLocalEvent]
    private void OnPoweredStaminaCost(Entity<StaminaDamageOnHitRequiresChargeComponent> ent,
        ref ModifyHitPowerCostEvent args)
    {
        args.Cost += TryComp<StunbatonComponent>(ent, out var legacy)
            ? legacy.EnergyPerUse
            : ent.Comp.RequiredCharge;
    }
}

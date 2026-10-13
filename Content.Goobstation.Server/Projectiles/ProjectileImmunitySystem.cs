using Content.Goobstation.Shared.Projectiles;
using Content.Server.Power.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._IS14.Cyborgs.Components; // IS14-change

namespace Content.Goobstation.Server.Projectiles;

public sealed class ProjectileImmunitySystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileImmunityComponent, ProjectileDodgeAttemptEvent>(OnDodgeAttempt);
    }

    private void OnDodgeAttempt(Entity<ProjectileImmunityComponent> ent, ref ProjectileDodgeAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.BatteryCostPerDodge <= 0)
            return;

        //IS14-change start - tgstation-style cyborgs count as borgs
        if (!(HasComp<BorgChassisComponent>(ent) || HasComp<CyborgComponent>(ent)))
        //IS14-change end
            return;

        if (!_battery.TryGetBatteryComponent(ent, out var batteryComp, out var battery))
        {
            args.Cancelled = true;
            return;
        }

        var cost = batteryComp.MaxCharge * (ent.Comp.BatteryCostPerDodge / 100);
        if (batteryComp.LastCharge < cost)
        {
            args.Cancelled = true;
            return;
        }

        _battery.UseCharge(battery.Value, cost);
    }
}

using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Damage.Systems;

public sealed class PassiveDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PassiveDamageComponent, MapInitEvent>(OnPendingMapInit);
        SubscribeLocalEvent<PassiveDamageComponent, DamageDealtEvent>(OnDamageTaken);
    }

    private void OnPendingMapInit(EntityUid uid, PassiveDamageComponent component, MapInitEvent args)
    {
        component.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(component.Interval);
        Dirty(uid, component);
    }

    //IS14-change: retain Wizards' interruption window alongside Goob healing.
    private void OnDamageTaken(Entity<PassiveDamageComponent> ent, ref DamageDealtEvent args)
    {
        if (ent.Comp.IntervalHaltOnDamageTaken <= TimeSpan.Zero || !args.Damage.AnyPositive())
            return;
        var next = _timing.CurTime + ent.Comp.IntervalHaltOnDamageTaken;
        if (next <= ent.Comp.NextDamage)
            return;
        ent.Comp.NextDamage = next;
        Dirty(ent);
    }

    // Every tick, attempt to damage entities
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _timing.CurTime;

        // Go through every entity with the component
        var query = EntityQueryEnumerator<PassiveDamageComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damage))
        {
            // Make sure they're up for a damage tick
            if (comp.NextDamage > curTime)
                continue;

            if (comp.DamageCap != 0 && damage.TotalDamage >= comp.DamageCap)
                continue;

            // Set the next time they can take damage
            comp.NextDamage = curTime + TimeSpan.FromSeconds(comp.Interval);
            Dirty(uid, comp);

            // Goobstation
            if (comp.AllowedStates == null || !TryComp<MobStateComponent>(uid, out var mobState))
            {
                _damageable.TryChangeDamage(uid, comp.Damage, true, false);
                continue; //IS14-change: process other due entities in the same tick.
            }

            // Damage them
            foreach (var allowedState in comp.AllowedStates)
            {
                if(allowedState == mobState.CurrentState)
                    _damageable.ChangeDamage((uid, damage), comp.Damage, true, false, targetPart: TargetBodyPart.All, splitDamage: comp.SplitBehavior); // Goob - Shitmed Change
            }
        }
    }
}

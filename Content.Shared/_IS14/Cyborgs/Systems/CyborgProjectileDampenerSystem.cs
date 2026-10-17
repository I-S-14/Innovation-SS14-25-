// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Projectiles;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgProjectileDampenerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedCyborgSystem _cyborg = default!;

    private readonly HashSet<Entity<ProjectileComponent>> _projectiles = new();
    private TimeSpan _nextDrain;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgProjectileDampenerComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<CyborgProjectileDampenerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        _popup.PopupClient(Loc.GetString(ent.Comp.Active ? "cyborg-dampener-on" : "cyborg-dampener-off"), ent,
            args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Projectile damage and velocity are server authoritative.
        if (_net.IsClient)
            return;

        var drain = _timing.CurTime >= _nextDrain;
        if (drain)
            _nextDrain = _timing.CurTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<CyborgProjectileDampenerComponent, CyborgToolComponent>();
        while (query.MoveNext(out var uid, out var dampener, out var tool))
        {
            if (!dampener.Active || tool.Cyborg is not { } cyborg ||
                !TryComp<CyborgComponent>(cyborg, out var cyborgComp))
                continue;

            // A stored tool or a dead cell switches the field off.
            if (!_cyborg.IsToolHeld((cyborg, cyborgComp), uid, out _) ||
                drain && !_powerCell.TryUseCharge(cyborg, dampener.ChargePerSecond))
            {
                dampener.Active = false;
                Dirty(uid, dampener);
                continue;
            }

            _projectiles.Clear();
            _lookup.GetEntitiesInRange(Transform(cyborg).Coordinates, dampener.Range, _projectiles);
            foreach (var projectile in _projectiles)
            {
                // The cyborg's own shots pass untouched.
                if (projectile.Comp.Shooter == cyborg || HasComp<CyborgDampenedProjectileComponent>(projectile))
                    continue;

                AddComp<CyborgDampenedProjectileComponent>(projectile);
                projectile.Comp.Damage *= dampener.DamageMultiplier;

                if (TryComp<PhysicsComponent>(projectile, out var physics))
                    _physics.SetLinearVelocity(projectile, physics.LinearVelocity * dampener.SpeedMultiplier,
                        body: physics);
            }
        }
    }
}

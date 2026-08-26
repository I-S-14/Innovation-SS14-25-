// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Fires the pepper burst when somebody hits or shoves the wearer.
///
///     The marker lives on the wearer rather than on the suit because both events are
///     raised on the person: a shove never reaches the inventory relay, and hanging the
///     module off the person is the only place that sees both.
/// </summary>
public sealed class ModulePepperSystem : ModuleBehaviourSystem<ModulePepperComponent>
{
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedChassisModuleSystem _modules = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChassisPepperGuardComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<ChassisPepperGuardComponent, DisarmedEvent>(OnDisarmed);
    }

    protected override void Start(Entity<ModulePepperComponent> ent, EntityUid chassis)
    {
        if (GetChassisUser(chassis) is not { } user)
            return;

        var guard = EnsureComp<ChassisPepperGuardComponent>(user);
        guard.Module = ent;
    }

    protected override void Stop(Entity<ModulePepperComponent> ent, EntityUid chassis)
    {
        if (GetChassisUser(chassis) is not { } user || TerminatingOrDeleted(user))
            return;

        // Another suit's module may have claimed the wearer in the meantime.
        if (TryComp<ChassisPepperGuardComponent>(user, out var guard) && guard.Module == ent.Owner)
            RemComp<ChassisPepperGuardComponent>(user);
    }

    private void OnAttacked(Entity<ChassisPepperGuardComponent> ent, ref AttackedEvent args)
    {
        TryBurst(ent, args.User);
    }

    private void OnDisarmed(Entity<ChassisPepperGuardComponent> ent, ref DisarmedEvent args)
    {
        TryBurst(ent, args.Source);
    }

    /// <summary>
    ///     One burst, if the module is off cooldown and the suit can pay for it.
    ///     Silent when it cannot: the attacker should not be told the suit is dry.
    /// </summary>
    private void TryBurst(Entity<ChassisPepperGuardComponent> ent, EntityUid attacker)
    {
        if (attacker == ent.Owner)
            return;

        if (!TryComp<ModulePepperComponent>(ent.Comp.Module, out var pepper)
            || !TryComp<ChassisModuleComponent>(ent.Comp.Module, out var module)
            || !module.Active
            || module.Chassis is not { } chassis)
            return;

        if (_modules.GetCooldownRemaining(module) > TimeSpan.Zero)
            return;

        if (module.UseCost > 0f && !_power.TryUseCharge(chassis, module.UseCost))
            return;

        _modules.StartCooldown((ent.Comp.Module, module));

        // Spawning is the server's business; the cloud is a real entity with a life of
        // its own rather than an effect.
        if (_net.IsServer)
            Spawn(pepper.Burst, _transform.GetMapCoordinates(ent.Owner));
    }
}

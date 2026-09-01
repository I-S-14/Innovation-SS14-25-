// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Common.Stunnable;
using Content.Shared._IS14.Modular.Systems;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Eats the stun a baton would have landed, as long as the core can pay for it.
/// </summary>
public sealed class ModuleStunAbsorberSystem : ModuleBehaviourSystem<ModuleStunAbsorberComponent>
{
    [Dependency] private readonly ChassisPowerSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunAbsorbedComponent, BeforeStunEvent>(OnBeforeStun);
    }

    private void OnBeforeStun(Entity<StunAbsorbedComponent> ent, ref BeforeStunEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ModuleStunAbsorberComponent>(ent.Comp.Module, out var absorber)
            || GetChassis(ent.Comp.Module) is not { } chassis)
        {
            return;
        }

        // A flat core cost per hit. Run the suit dry and the baton works again, which is
        // the point: the module buys time, it does not make the wearer immune.
        if (!_power.TryUseCharge(chassis, absorber.Cost))
            return;

        args.Cancelled = true;
    }

    protected override void Start(Entity<ModuleStunAbsorberComponent> ent, EntityUid chassis)
    {
        if (GetChassisUser(chassis) is not { } user)
            return;

        if (ent.Comp.GrantedTo == user)
            return;

        Revoke(ent);

        EnsureComp<StunAbsorbedComponent>(user).Module = ent.Owner;
        ent.Comp.GrantedTo = user;
    }

    protected override void Stop(Entity<ModuleStunAbsorberComponent> ent, EntityUid chassis)
    {
        Revoke(ent);
    }

    protected override void UserChanged(Entity<ModuleStunAbsorberComponent> ent, EntityUid chassis, EntityUid? user)
    {
        if (ent.Comp.GrantedTo == null)
            return;

        if (user == null)
            Revoke(ent);
        else
            Start(ent, chassis);
    }

    private void Revoke(Entity<ModuleStunAbsorberComponent> ent)
    {
        if (ent.Comp.GrantedTo is not { } previous)
            return;

        ent.Comp.GrantedTo = null;

        // The wearer may already be gone — gibbed, deleted, or simply never existed by
        // the time a suit was taken apart.
        if (!TerminatingOrDeleted(previous))
            RemComp<StunAbsorbedComponent>(previous);
    }
}

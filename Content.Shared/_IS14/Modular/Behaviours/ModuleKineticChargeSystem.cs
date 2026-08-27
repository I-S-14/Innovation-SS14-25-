// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Gravity;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Ratchets in the suit's joints, turning the wearer's own stride back into charge.
/// </summary>
public sealed class ModuleKineticChargeSystem : ModuleBehaviourSystem<ModuleKineticChargeComponent>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void Start(Entity<ModuleKineticChargeComponent> ent, EntityUid chassis)
    {
        ent.Comp.Running = true;

        // Sampled fresh rather than carried over: the suit may have travelled a long way
        // in somebody's backpack since it was last switched on.
        ent.Comp.LastPosition = null;
    }

    protected override void Stop(Entity<ModuleKineticChargeComponent> ent, EntityUid chassis)
    {
        ent.Comp.Running = false;
        ent.Comp.LastPosition = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Charge is the server's to hand out; predicting it would only fight the state.
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<ModuleKineticChargeComponent, ChassisModuleComponent>();

        while (query.MoveNext(out var uid, out var kinetic, out var module))
        {
            if (!kinetic.Running || module.Chassis is not { } chassis)
                continue;

            Sample((uid, kinetic), chassis);
        }
    }

    private void Sample(Entity<ModuleKineticChargeComponent> ent, EntityUid chassis)
    {
        var user = GetChassisUser(chassis);

        // Nobody in the suit, nothing to ratchet.
        if (user == null || TerminatingOrDeleted(user.Value))
        {
            ent.Comp.LastPosition = null;
            return;
        }

        var position = _transform.GetMapCoordinates(user.Value);

        // Ratchets need something to push off. Floating, the joints move freely and the
        // module reads nothing — which is also what stops it paying for the jetpack.
        if (_gravity.IsWeightless(user.Value))
        {
            ent.Comp.LastPosition = position;
            return;
        }

        var previous = ent.Comp.LastPosition;
        ent.Comp.LastPosition = position;

        if (previous is not { } last || last.MapId != position.MapId)
            return;

        var distance = (position.Position - last.Position).Length();

        // A step this large was not walked. Shuttle docking, teleport, being thrown.
        if (distance <= 0f || distance > ent.Comp.MaxStep)
            return;

        _power.TryAddCharge(chassis, distance * ent.Comp.ChargePerMetre);
    }
}

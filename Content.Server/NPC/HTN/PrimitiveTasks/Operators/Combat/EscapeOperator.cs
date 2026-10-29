// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Storage.Components; // Goob - Fix (non-storage containers)
using Content.Shared.Storage.Components;
using Robust.Server.Containers;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat;

public sealed partial class EscapeOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedCombatModeSystem _combatModeSystem = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Goob - Fix (non-storage containers)
        if (!_entManager.HasComponent<EntityStorageComponent>(target)
            || _entityStorage.TryOpenStorage(owner, target))
        {
            TaskShutdown(blackboard, HTNOperatorStatus.Finished);
            return;

        var melee = _entManager.EnsureComponent<NPCMeleeCombatComponent>(owner);
        melee.MissChance = blackboard.GetValueOrDefault<float>(NPCBlackboard.MeleeMissChance, _entManager);
        melee.Target = container.Owner;
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_container.TryGetContainingContainer(owner, out var container))
        {
            return (false, null);
        }

        if (!_container.IsEntityInContainer(owner))
        {
            return (false, null);
        }

        // Goob - Fix (non-storage containers)
        if (!_entManager.HasComponent<EntityStorageComponent>(target))
        {
            return (false, null);
        }

        if (_entityStorage.TryOpenStorage(owner, target))
        {
            // We must be in a backpack or something that we can't open or attack to escape from.
            // It could be possible to mirror some of the Resist.EscapeInventorySystem logic in this case.
            return (false, null);
        }

        return (true, null);
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        _combatModeSystem.SetInCombatMode(owner, false);
        _entManager.RemoveComponent<NPCMeleeCombatComponent>(owner);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);

        ConditionalShutdown(blackboard);
    }

    public override void PlanShutdown(NPCBlackboard blackboard)
    {
        base.PlanShutdown(blackboard);

        ConditionalShutdown(blackboard);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_container.TryGetContainingContainer(owner, out var container)
            || _entityStorage.TryOpenStorage(owner, container.Owner))
        {
            combat.Target = target;

            // Success
            if (!_container.IsEntityInContainer(owner))
            {
                status = HTNOperatorStatus.Finished;
            }
            else if (!_entManager.HasComponent<EntityStorageComponent>(target)) // Goob - Fix (non-storage containers)
            {
                status = HTNOperatorStatus.Failed;
            }
            else
            {
                if (_entityStorage.TryOpenStorage(owner, target))
                {
                    status = HTNOperatorStatus.Finished;
                }
                else
                {
                    switch (combat.Status)
                    {
                        case CombatStatus.TargetOutOfRange:
                        case CombatStatus.Normal:
                            status = HTNOperatorStatus.Continuing;
                            break;
                        default:
                            status = HTNOperatorStatus.Failed;
                            break;
                    }
                }
            }
        }
        else
        {
            status = HTNOperatorStatus.Failed;
        }

        // We failed to open it... Perhaps violence is the answer?
        if (!_entManager.TryGetComponent<NPCMeleeCombatComponent>(owner, out var combat))
        {
            return HTNOperatorStatus.Failed;
        }

        combat.Target = container.Owner;

        switch (combat.Status)
        {
            case CombatStatus.TargetOutOfRange:
            case CombatStatus.Normal:
                return HTNOperatorStatus.Continuing;
            default:
                return HTNOperatorStatus.Failed;
        }
    }
}
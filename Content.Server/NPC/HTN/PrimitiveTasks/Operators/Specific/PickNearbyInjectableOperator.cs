// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Interaction;
using Content.Shared.Silicons.Bots;
using Content.Shared.Stealth.Components; // Goobstation
using Content.Shared.Emag.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class PickNearbyInjectableOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private MedibotSystem _medibot = default!;
    [Dependency] private PathfindingSystem _pathfinding = default!;

    [DataField("rangeKey")] public string RangeKey = NPCBlackboard.MedibotInjectRange;

    /// <summary>
    /// Target entity to inject
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    /// <summary>
    /// Target entitycoordinates to move to.
    /// </summary>
    [DataField("targetMoveKey", required: true)]
    public string TargetMoveKey = string.Empty;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<float>(RangeKey, out _, _entManager))
            return (false, null);

        if (!_entManager.TryGetComponent<MedibotComponent>(owner, out var medibot))
            return (false, null);

        var damageQuery = _entManager.GetEntityQuery<DamageableComponent>();
        var injectQuery = _entManager.GetEntityQuery<InjectableSolutionComponent>();
        var recentlyInjected = _entManager.GetEntityQuery<NPCRecentlyInjectedComponent>();
        var mobState = _entManager.GetEntityQuery<MobStateComponent>();
        var emaggedQuery = _entManager.GetEntityQuery<EmaggedComponent>();
        var stealthQuery = _entManager.GetEntityQuery<StealthComponent>(); // Goobstation

        foreach (var (entity, _) in patients)
        {
            if (mobState.TryGetComponent(entity, out var state) &&
                injectQuery.HasComponent(entity) &&
                damageQuery.TryGetComponent(entity, out var damage) &&
                !recentlyInjected.HasComponent(entity) &&
                !(stealthQuery.TryGetComponent(entity, out var stealth) && stealth.Enabled)) // Goobstation - stealth check
            {
                { TargetKey, entity },
                { TargetMoveKey, _entManager.GetComponent<TransformComponent>(entity).Coordinates },
                { NPCBlackboard.PathfindKey, path },
            });
        }

        return (false, null);
    }
}

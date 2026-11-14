using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Silicon.Bots;

using Content.Server.Botany.Systems;
using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class PlantbotServiceOperator : HTNOperator
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private ChatSystem _chat = default!;
    private SharedAudioSystem _audio = default!;
    private SharedInteractionSystem _interaction = default!;
    private SharedPopupSystem _popup = default!;
    private PlantTraySystem _plantHolderSystem = default!;

    public const float RequiredWaterLevelToService = 80f;
    public const float RequiredWeedsAmountToWeed = 1f;
    public const float WaterTransferAmount = 10f;
    public const float WeedsRemovedAmount = 1f;
    public const string SiliconTag = "SiliconMob";

    /// <summary>
    /// Target entity to inject.
    /// </summary>
    [DataField(required: true)]
    public string TargetKey = string.Empty;

    public override void Initialize(IDependencyCollection deps)
    {
        base.Initialize(deps);
        var sysManager = deps.Resolve<IEntitySystemManager>();

        _chat = sysManager.GetEntitySystem<ChatSystem>();
        _audio = sysManager.GetEntitySystem<SharedAudioSystem>();
        _interaction = sysManager.GetEntitySystem<SharedInteractionSystem>();
        _popup = sysManager.GetEntitySystem<SharedPopupSystem>();
        _plantHolderSystem = sysManager.GetEntitySystem<PlantTraySystem>();
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entMan) || _entMan.Deleted(target))
            return HTNOperatorStatus.Failed;

        if (!_entMan.TryGetComponent<PlantbotComponent>(owner, out var botComp)
            || !_entMan.TryGetComponent<PlantTrayComponent>(target, out var plantHolderComponent)
            || !_interaction.InRangeUnobstructed(owner, target))
            return HTNOperatorStatus.Failed;

        var dead = plantHolderComponent.PlantEntity is { } plant
            && _entMan.TryGetComponent<PlantHolderComponent>(plant, out var holder) && holder.Dead;
        if (plantHolderComponent is { WaterLevel: >= RequiredWaterLevelToService, WeedLevel: <= RequiredWeedsAmountToWeed }
            && (!_entMan.HasComponent<EmaggedComponent>(owner) || dead || plantHolderComponent.WaterLevel <= 0f))
            return HTNOperatorStatus.Failed;

        if (botComp.IsEmagged)
        {
            _plantHolderSystem.AdjustWater(target, -WaterTransferAmount);
            _audio.PlayPvs(botComp.RemoveWaterSound, target);
        }
        else
        {
            if (plantHolderComponent.WaterLevel <= RequiredWaterLevelToService)
            {
                _plantHolderSystem.AdjustWater(target, 10);
                _audio.PlayPvs(botComp.WaterSound, target);
                _chat.TrySendInGameICMessage(owner, Loc.GetString("plantbot-add-water"), InGameICChatType.Speak, hideChat: true, hideLog: true);
            }
            else if (plantHolderComponent.WeedLevel >= RequiredWeedsAmountToWeed)
            {
                _plantHolderSystem.AdjustWeed(target, -WeedsRemovedAmount);
                _audio.PlayPvs(botComp.WeedSound, target);
                _chat.TrySendInGameICMessage(owner, Loc.GetString("plantbot-remove-weeds"), InGameICChatType.Speak, hideChat: true, hideLog: true);
            }
            else
                return HTNOperatorStatus.Failed;
        }

        return HTNOperatorStatus.Finished;
    }
}

using Content.Shared.Timing.Systems;
using Robust.Shared.GameStates;
using Content.Shared.Timing;

namespace Content.Shared.Trigger.Components.Conditions;

/// <summary>
/// Checks if the triggered entity has an active UseDelay.
/// </summary>
/// <remarks>
/// TODO: Support specific UseDelay IDs for each trigger key.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UseDelayTriggerConditionComponent : BaseTriggerConditionComponent
{
    /// <summary>
    /// Checks if the triggered entity has an active UseDelay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string UseDelayId = UseDelaySystem.DefaultId;
}

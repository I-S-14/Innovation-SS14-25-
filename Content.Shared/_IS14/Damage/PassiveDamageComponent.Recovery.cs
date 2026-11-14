using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

public sealed partial class PassiveDamageComponent
{
    /// <summary>Delay passive recovery after taking positive damage.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan IntervalHaltOnDamageTaken;
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// A stack built into a cyborg, like tgstation's iron or cable synthesizer.
/// It refills from the cyborg's power cell and never runs out completely,
/// so the tool doesn't vanish from the kit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CyborgStackSynthesizerComponent : Component
{
    /// <summary>
    /// Power cell charge spent per synthesized unit.
    /// </summary>
    [DataField]
    public float ChargePerUnit = 5f;

    /// <summary>
    /// How often one unit is synthesized while the stack is below its maximum.
    /// </summary>
    [DataField]
    public TimeSpan RegenDelay = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRegen = TimeSpan.Zero;
}

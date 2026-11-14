// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// Reports the cyborg to robotics consoles and takes their disable, destroy and law commands.
/// Speaks the same network protocol as the upstream borg transponder.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CyborgTransponderComponent : Component
{
    /// <summary>
    /// Chassis name shown on the console.
    /// </summary>
    [DataField]
    public LocId ChassisName = "cyborg-transponder-chassis-name";

    [DataField]
    public TimeSpan BroadcastDelay = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextBroadcast = TimeSpan.Zero;

    /// <summary>
    /// When the brain gets ejected after a disable command. Null while no disable is pending.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextDisable;

    [DataField]
    public TimeSpan DisableDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// An emagged cyborg only pretends to be disabled.
    /// </summary>
    [DataField]
    public bool FakeDisabling;

    [DataField]
    public bool FakeDisabled;
}

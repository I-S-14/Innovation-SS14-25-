// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     An <see cref="ModuleKind.Active"/> module that keeps a device entity and puts it
///     into the operator's hands while selected — defibrillator paddles, a health analyzer,
///     a mining drill, a hydraulic clamp.
///
///     The device is never destroyed on retraction; it lives in the module's own container
///     so its state (charge, loaded contents) survives being stowed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleItemComponent : Component
{
    /// <summary>
    ///     Device spawned on map init.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId DevicePrototype = default!;

    /// <summary>
    ///     Container the device sits in while stowed.
    /// </summary>
    [DataField]
    public string ContainerId = "module-device";

    /// <summary>
    ///     Populated on init, but null until then — resolve it through the system.
    /// </summary>
    [ViewVariables]
    public ContainerSlot? Container;

    /// <summary>
    ///     The live device entity.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Device;

    /// <summary>
    ///     Who is currently holding the device.
    /// </summary>
    [ViewVariables]
    public EntityUid? HeldBy;
}

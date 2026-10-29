using Content.Shared.DeviceNetwork;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.Shared.Robotics;

[Serializable, NetSerializable]
public enum RoboticsConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class RoboticsConsoleState : BoundUserInterfaceState
{
    /// <summary>
    /// Map of device network addresses to cyborg data.
    /// </summary>
    public Dictionary<LocDeviceAddress, CyborgControlData> Cyborgs;

    /// <summary>
    /// If the UI will have the buttons to disable and destroy.
    /// </summary>
    public bool AllowBorgControl;

    public bool HasCircuitBoard; // Corvax-Goob-MutableLaws

    public RoboticsConsoleState(Dictionary<LocDeviceAddress, CyborgControlData> cyborgs, bool allowBorgControl, bool hasCircuitBoard = false) // Corvax-Goob-MutableLaws
    {
        Cyborgs = cyborgs;
        AllowBorgControl = allowBorgControl;
        HasCircuitBoard = hasCircuitBoard; // Corvax-Goob-MutableLaws
    }
}

// Corvax-Goob-MutableLaws-Start
[Serializable, NetSerializable]
public sealed class RoboticsConsoleChangeLawsMessage(DeviceAddress address) : BoundUserInterfaceMessage
{
    public readonly DeviceAddress Address = address;
}
// Corvax-Goob-MutableLaws-End

/// <summary>
/// Message to disable the selected cyborg.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoboticsConsoleDisableMessage : BoundUserInterfaceMessage
{
    public readonly DeviceAddress Address;

    public RoboticsConsoleDisableMessage(DeviceAddress address)
    {
        Address = address;
    }
}

/// <summary>
/// Message to destroy the selected cyborg.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoboticsConsoleDestroyMessage : BoundUserInterfaceMessage
{
    public readonly DeviceAddress Address;

    public RoboticsConsoleDestroyMessage(DeviceAddress address)
    {
        Address = address;
    }
}

/// <summary>
/// All data a client needs to render the console UI for a single cyborg.
/// Created by <c>BorgTransponderComponent</c> and sent to clients by <c>RoboticsConsoleComponent</c>.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct CyborgControlData
{
    /// <summary>
    /// Texture of the borg chassis.
    /// </summary>
    [DataField(required: true)]
    public SpriteSpecifier? ChassisSprite;

    /// <summary>
    /// Name of the borg chassis.
    /// </summary>
    [DataField(required: true)]
    public string ChassisName = string.Empty;

    /// <summary>
    /// Name of the borg's entity, including its silicon id.
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    /// <summary>
    /// Battery charge from 0 to 1.
    /// </summary>
    [DataField]
    public float Charge;

    /// <summary>
    /// HP level from 0 to 1.
    /// </summary>
    [DataField]
    public float HpPercent; // 0.0 to 1.0

    /// <summary>
    /// How many modules this borg has, just useful information for roboticists.
    /// Lets them keep track of the latejoin borgs that need new modules and stuff.
    /// </summary>
    [DataField]
    public int ModuleCount;

    /// <summary>
    /// Whether the borg has a brain installed or not.
    /// </summary>
    [DataField]
    public bool HasBrain;

    /// <summary>
    /// Whether the borg can currently be disabled if the brain is installed,
    /// if on cooldown then can't queue up multiple disables.
    /// </summary>
    [DataField]
    public bool CanDisable;

    /// <summary>
    /// When this cyborg's data will be deleted.
    /// Set by the console when receiving the packet.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan Timeout = TimeSpan.Zero;

    // Corvax-Next-AiRemoteControl-Start
    [DataField]
    public bool IsAiControllable;
    // Corvax-Next-AiRemoteControl-End

    // CorvaxGoob-MutableLaws
    [DataField]
    public bool IsLawChangable;

    public CyborgControlData(SpriteSpecifier? chassisSprite, string chassisName, string name, float charge, float hpPercent, int moduleCount, bool hasBrain, bool canDisable, bool isAiControllable = false, bool isLawChangable = false) // Corvax-Next-AiRemoteControl && CorvaxGoob-MutableLaws
    {
        IsAiControllable = isAiControllable; // Corvax-Next-AiRemoteControl
        IsLawChangable = isLawChangable; // CorvaxGoob-MutableLaws
        ChassisSprite = chassisSprite;
        ChassisName = chassisName;
        Name = name;
        Charge = charge;
        HpPercent = hpPercent;
        ModuleCount = moduleCount;
        HasBrain = hasBrain;
        CanDisable = canDisable;
    }
}

/// <summary>
/// Disables a borg when received.
/// </summary>
public partial record struct RoboticsCyborgDisablePayload : INetworkPayload;

/// <summary>
/// Destroys a borg when received.
/// </summary>
public partial record struct RoboticsCyborgDestroyPayload : INetworkPayload;

/// <summary>
/// Corvax-Goob-MutableLaws: tells a borg to take the law set of the console's circuit board.
/// Replaces the old "cyborg-change-laws" string command.
/// </summary>
public partial record struct RoboticsCyborgChangeLawsPayload : INetworkPayload;

/// <summary>
/// A wrapper for <see cref="CyborgControlData"/>
/// </summary>
public partial record struct RoboticsCyborgDataPayload : INetworkPayload
{
    [DataField]
    public CyborgControlData Data;
}

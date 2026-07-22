using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.VendingMachine;

[Serializable, NetSerializable]
public enum IS14VendingMachineVisuals : byte
{
    VisualState,
}

[Serializable, NetSerializable]
public enum IS14VendingMachineVisualState : byte
{
    Normal,
    Off,
    Broken,
    Deny,
}

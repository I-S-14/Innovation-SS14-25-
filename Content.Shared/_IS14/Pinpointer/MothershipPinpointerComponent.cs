// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Pinpointer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MothershipPinpointerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Selected;

    /// <summary>Find a core automatically until the user makes an explicit selection.</summary>
    [DataField]
    public bool AutoCore = true;
}

[Serializable, NetSerializable]
public enum MothershipPinpointerUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class MothershipPinpointerSelectMessage(NetEntity? target) : BoundUserInterfaceMessage
{
    /// <summary>Null switches the tracker off.</summary>
    public NetEntity? Target = target;
}

[Serializable, NetSerializable]
public sealed class MothershipPinpointerState(List<(NetEntity Entity, string Name)> targets, NetEntity? selected, bool active)
    : BoundUserInterfaceState
{
    public List<(NetEntity Entity, string Name)> Targets = targets;
    public NetEntity? Selected = selected;
    public bool Active = active;
}

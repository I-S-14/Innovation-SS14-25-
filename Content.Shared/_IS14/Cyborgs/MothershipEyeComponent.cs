// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Cyborgs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MothershipEyeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Eye;

    [DataField, AutoNetworkedField]
    public EntityUid? Following;

    [DataField]
    public float ViewRange = 7f;

    /// <summary>Visibility bits enabled by observation, removed when returning to the body.</summary>
    [ViewVariables]
    public int AddedVisibilityMask;
}

[Serializable, NetSerializable]
public enum MothershipEyeUiKey : byte { Key }

[Serializable, NetSerializable]
public enum MothershipEyeCommand : byte { Ship, Return, Jump, Follow }

[Serializable, NetSerializable]
public sealed class MothershipEyeMessage(MothershipEyeCommand command, NetEntity? target = null)
    : BoundUserInterfaceMessage
{
    public MothershipEyeCommand Command = command;
    public NetEntity? Target = target;
}

[Serializable, NetSerializable]
public sealed class MothershipEyeState(List<(NetEntity Entity, string Name)> borgs, bool active, string? following)
    : BoundUserInterfaceState
{
    public List<(NetEntity Entity, string Name)> Borgs = borgs;
    public bool Active = active;
    public string? Following = following;
}

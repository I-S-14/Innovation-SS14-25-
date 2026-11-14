// SPDX-License-Identifier: MIT
using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System.Threading;
using Robust.Shared.Serialization;

namespace Content.Shared.Singularity.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmitterComponent : Component
{

    [DataField]
    public List<EntProtoId> SelectableTypes = new();

    /// <summary>
    /// Map of signal ports to entity prototype IDs of the entity that will be fired.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SinkPortPrototype>, EntProtoId> SetTypePorts = new();

    // IS14-merge: restored from Goob/IS14 version
    /// <summary>
    /// The entity that is spawned when the emitter fires.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId BoltType = "EmitterBolt";
}

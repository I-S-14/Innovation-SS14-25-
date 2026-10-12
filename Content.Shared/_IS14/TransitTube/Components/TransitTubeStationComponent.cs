using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.TransitTube.Components;

/// <summary>
/// A tube section pods stop at, open up in, and are launched from. Stations connect
/// perpendicular to their rotation, so the open side faces the corridor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransitTubeStationComponent : Component
{
    /// <summary>
    /// A terminus only connects on one side and turns pods around instead of letting
    /// them pass through.
    /// </summary>
    [DataField]
    public bool Terminus;

    /// <summary>
    /// How long the doors take to open, in seconds. Should match the sprite animation.
    /// </summary>
    [DataField]
    public float OpenTime = 0.5f;

    /// <summary>
    /// How long the doors take to close, in seconds. Should match the sprite animation.
    /// </summary>
    [DataField]
    public float CloseTime = 0.5f;

    /// <summary>
    /// How long a pod waits with its doors open before the station sends it on. Long
    /// enough to climb in, short enough that waiting for the pod is not a chore.
    /// </summary>
    [DataField]
    public TimeSpan Dwell = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Bumping into an open station boards you, so somebody who just climbed out needs a
    /// moment to walk away before the station grabs them again.
    /// </summary>
    [DataField]
    public TimeSpan BoardCooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// When bumping into this station may board a pod again.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextBoard;

    /// <summary>
    /// Which way this stop sends a pod that is starting its ride here: false is 90 degrees
    /// clockwise of the station's facing, true is the other way. Pods passing through keep
    /// going the way they came, so this only decides where new rides head off to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PreferCounterClockwise;

    /// <summary>
    /// The pod this station conjures up for anyone who steps in while it has none. Clear it
    /// to get a station that only ever serves pods that already exist.
    /// </summary>
    [DataField]
    public EntProtoId? Dispenses = "IS14TransitTubePod";

    /// <summary>
    /// The pod parked in this station, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? Pod;

    [ViewVariables]
    public TransitTubeStationState State = TransitTubeStationState.Closed;

    /// <summary>
    /// When the door animation currently playing finishes.
    /// </summary>
    [ViewVariables]
    public TimeSpan StateEnd;

    /// <summary>
    /// The earliest time the parked pod may be launched.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextLaunch;

    [DataField]
    public SoundSpecifier? ArriveSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    [DataField]
    public SoundSpecifier? DispenseSound =
        new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg", AudioParams.Default.WithVolume(-4f));

    [DataField]
    public SoundSpecifier? DissolveSound =
        new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg", AudioParams.Default.WithVolume(-6f));

    [DataField]
    public SoundSpecifier? DoorSound =
        new SoundPathSpecifier("/Audio/Machines/airlock_ext_open.ogg", AudioParams.Default.WithVolume(-6f));
}

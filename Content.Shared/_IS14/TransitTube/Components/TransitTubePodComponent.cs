using Content.Shared.Atmos;
using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.TransitTube.Components;

/// <summary>
/// The capsule that carries crew and cargo through the tubes. Unlike a disposal holder
/// the pod is a persistent, visible entity: it waits inside a station until the station
/// launches it, rides the tubes, and stops at the next station it meets.
/// </summary>
[RegisterComponent]
public sealed partial class TransitTubePodComponent : Component
{
    public const string ContainerId = "transit_tube_pod";

    /// <summary>
    /// Whatever is riding inside the pod.
    /// </summary>
    [ViewVariables]
    public Container Container = default!;

    /// <summary>
    /// The tube section the pod currently occupies. Null means the pod is loose on the grid.
    /// </summary>
    [ViewVariables]
    public EntityUid? CurrentTube;

    /// <summary>
    /// The direction the pod is travelling in, used to pick the next section.
    /// </summary>
    [ViewVariables]
    public Direction CurrentDirection = Direction.Invalid;

    /// <summary>
    /// The direction the pod travelled in to reach <see cref="CurrentTube"/>.
    /// </summary>
    [ViewVariables]
    public Direction PreviousDirection = Direction.Invalid;

    /// <summary>
    /// Whether the pod is currently sliding between sections.
    /// </summary>
    [ViewVariables]
    public bool Moving;

    /// <summary>
    /// Total travel time of the hop currently in progress, in seconds.
    /// </summary>
    [ViewVariables]
    public float StartingTime;

    /// <summary>
    /// Time left of the hop currently in progress, in seconds.
    /// </summary>
    [ViewVariables]
    public float TimeLeft;

    /// <summary>
    /// A pod the station conjured up for a passenger. It melts away again as soon as it is
    /// empty and parked, so stations are not left littered with pods nobody is riding.
    /// </summary>
    [DataField]
    public bool Temporary;

    /// <summary>
    /// When a pod without a section next looks for one under itself.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextDockCheck;

    /// <summary>
    /// Guard against ejecting the same pod twice in one frame.
    /// </summary>
    [ViewVariables]
    public bool Ejecting;

    /// <summary>
    /// The pod is sealed, so it carries its own air. Stations swap it for room air every
    /// time they open up, which is the only thing keeping a rider breathing.
    /// </summary>
    [DataField]
    public GasMixture Air = new(200f);

    [DataField]
    public SoundSpecifier? LaunchSound =
        new SoundPathSpecifier("/Audio/Machines/airlock_ext_open.ogg", AudioParams.Default.WithVolume(-4f));
}

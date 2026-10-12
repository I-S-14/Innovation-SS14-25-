namespace Content.Server._IS14.TransitTube.Components;

/// <summary>
/// Put on whoever is riding inside a pod, so that they breathe the pod's own air
/// instead of whatever is outside the tube.
/// </summary>
[RegisterComponent]
public sealed partial class TransitTubeRiderComponent : Component
{
    [ViewVariables]
    public EntityUid Pod;
}

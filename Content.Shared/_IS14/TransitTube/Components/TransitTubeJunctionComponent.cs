using Robust.Shared.GameStates;

namespace Content.Shared._IS14.TransitTube.Components;

/// <summary>
/// A tube section where two diagonal runs merge into one straight run. A pod keeps going
/// as straight as it can, so when both branches are equally sharp a turn, the preferred
/// one wins.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransitTubeJunctionComponent : Component
{
    /// <summary>
    /// Which branch a pod takes when it could take either. False is the branch 45 degrees
    /// clockwise of the straight run, which is the one the arrow on the plain picture
    /// points at; true is the other, and the section is drawn mirrored to match.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PreferCounterClockwise;
}

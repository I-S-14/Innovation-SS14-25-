using Robust.Shared.GameStates;

namespace Content.Shared._IS14.TransitTube.Components;

/// <summary>
/// A tube section that turns 45 degrees. It joins the end it faces to the one 135 degrees
/// round from it, which is the corner between a straight run and a diagonal one.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransitTubeBendComponent : Component
{
    /// <summary>
    /// Reckons the bend 45 degrees round, giving the four corners that lie between the
    /// ones a quarter turn can reach.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Diagonal;
}

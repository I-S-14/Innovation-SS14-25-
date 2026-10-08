namespace Content.Shared._IS14.TransitTube.Events;

/// <summary>
/// Raised on a tube section to ask which directions it can connect to.
/// </summary>
[ByRefEvent]
public record struct GetTransitTubeConnectableDirectionsEvent
{
    public Direction[] Connectable;
}

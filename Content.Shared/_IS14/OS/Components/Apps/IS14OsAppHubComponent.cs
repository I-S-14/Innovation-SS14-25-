using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     Download state for the app store. Downloads take time on purpose: it makes the store a
///     place you visit deliberately, and gives the network something to interrupt later.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsAppHubComponent : Component
{
    [ViewVariables]
    public ProtoId<IS14OsAppPrototype>? Downloading;

    /// <summary>GQ transferred so far.</summary>
    [ViewVariables]
    public float Downloaded;

    /// <summary>GQ per second.</summary>
    [DataField]
    public float Speed = 1.5f;

    [ViewVariables]
    public string? Error;
}

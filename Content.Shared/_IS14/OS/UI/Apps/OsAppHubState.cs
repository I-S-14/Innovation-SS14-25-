using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     The store. The catalogue itself is read from prototypes on the client — only what the
///     server actually arbitrates travels: what this device may download, and the download
///     currently in flight.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsAppHubState : IS14OsAppState
{
    public List<ProtoId<IS14OsAppPrototype>> Catalog = new();
    public List<ProtoId<IS14OsAppPrototype>> AccessDenied = new();

    public ProtoId<IS14OsAppPrototype>? Downloading;

    /// <summary>0..1 of the downloading app's size.</summary>
    public float Progress;

    /// <summary>Loc id of the last failure, shown until dismissed.</summary>
    public string? Error;
}

[Serializable, NetSerializable]
public sealed class OsHubDownloadEvent : IS14OsAppEvent
{
    public ProtoId<IS14OsAppPrototype> App;

    public OsHubDownloadEvent(ProtoId<IS14OsAppPrototype> app)
    {
        App = app;
    }
}

[Serializable, NetSerializable]
public sealed class OsHubCancelEvent : IS14OsAppEvent
{
}

[Serializable, NetSerializable]
public sealed class OsHubDismissErrorEvent : IS14OsAppEvent
{
}

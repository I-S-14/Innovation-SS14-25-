using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

[Serializable, NetSerializable]
public sealed class OsCameraState : IS14OsAppState
{
    /// <summary>How many photos are already on the device, so the shutter can say when to stop.</summary>
    public int Photos;

    /// <summary>Loc id of the last shot's outcome.</summary>
    public string? Status;
}

/// <summary>
///     A captured frame, as PNG bytes. The client renders the viewport and encodes it; the
///     server only ever validates and stores.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsCameraCaptureEvent : IS14OsAppEvent
{
    public byte[] Data;

    public OsCameraCaptureEvent(byte[] data)
    {
        Data = data;
    }
}

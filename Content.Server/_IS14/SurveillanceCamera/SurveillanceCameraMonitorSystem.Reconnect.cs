namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraMonitorSystem
{
    private float _reconnectAccumulator;

    private void UpdateReconnections(float frameTime)
    {
        _reconnectAccumulator += frameTime;
        if (_reconnectAccumulator < HeartbeatDelay)
            return;
        _reconnectAccumulator = 0;

        var query = EntityQueryEnumerator<SurveillanceCameraMonitorComponent>();
        while (query.MoveNext(out var uid, out var monitor))
        {
            if (monitor.ReconnectAddress is not { } address || monitor.ActiveCamera != null)
                continue;
            if (monitor.Viewers.Count == 0)
            {
                monitor.ReconnectAddress = null;
                continue;
            }

            TrySwitchCameraByAddress(uid, address, monitor: monitor);
        }
    }
}

using Content.Shared.SurveillanceCamera.Components;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraMapSystem
{
    private float _mobileAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _mobileAccumulator += frameTime;
        if (_mobileAccumulator < 1f)
            return;
        _mobileAccumulator = 0;

        // A worn bodycam follows its parent's transform without moving locally.
        var query = EntityQueryEnumerator<SurveillanceCameraComponent>();
        while (query.MoveNext(out var uid, out var camera))
        {
            if (camera.Mobile)
                UpdateCameraMarker((uid, camera));
        }
    }
}

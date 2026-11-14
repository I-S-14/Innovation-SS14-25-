using Content.Shared.Light.Components;

namespace Content.Shared.Light.EntitySystems;

public sealed partial class ExpendableLightSystem
{
    public void BurnOut(Entity<ExpendableLightComponent> ent)
    {
        if (ent.Comp.CurrentState == ExpendableLightState.Dead)
            return;
        ent.Comp.CurrentState = ExpendableLightState.Fading;
        ent.Comp.StateExpiryTime = _timing.CurTime;
        UpdateLight(ent);
    }
}

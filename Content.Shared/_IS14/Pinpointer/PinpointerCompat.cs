using Content.Shared.Pinpointer;

namespace Content.Shared.Pinpointer;

//IS14-change start: preserve Goob multi-target tracking for callers of the new API.
public abstract partial class SharedPinpointerSystem
{
    public void SetTarget(Entity<PinpointerComponent?> ent, EntityUid? target) => SetTarget(ent.Owner, target, ent.Comp);
    public void SetDistance(Entity<PinpointerComponent?> ent, Distance distance) => SetDistance(ent.Owner, distance, ent.Comp);
    public bool TrySetArrowAngle(Entity<PinpointerComponent?> ent, Angle angle) => TrySetArrowAngle(ent.Owner, angle, ent.Comp);
    public void SetActive(Entity<PinpointerComponent?> ent, bool active) => SetActive(ent.Owner, active, ent.Comp);
    public bool TogglePinpointer(Entity<PinpointerComponent?> ent) => TogglePinpointer(ent.Owner, ent.Comp);
}
//IS14-change end

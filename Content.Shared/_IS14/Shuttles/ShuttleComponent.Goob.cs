using Content.Shared._NF.Shuttles.Events;

namespace Content.Shared.Shuttles.Components;

public sealed partial class ShuttleComponent
{
    [DataField]
    public Dictionary<InertiaDampeningMode, float> DampingModifiers = new()
    {
        [InertiaDampeningMode.Cruise] = 0.0075f,
        [InertiaDampeningMode.Dampen] = 0.25f,
        [InertiaDampeningMode.Anchor] = 2f,
        [InertiaDampeningMode.None] = 0.25f,
    };
}

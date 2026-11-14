// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's harm alarm: a deafening siren that rattles everyone nearby.
/// Emagged cyborgs knock people down with it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgHarmAlarmComponent : Component
{
    [DataField]
    public float Range = 5f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan EmaggedKnockdown = TimeSpan.FromSeconds(3);

    [DataField]
    public float ChargeCost = 30f;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_IS14/Cyborgs/harmalarm.ogg");
}

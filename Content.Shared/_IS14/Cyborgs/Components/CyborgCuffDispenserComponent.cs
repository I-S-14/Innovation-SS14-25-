// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's cyborg zipties: never run out, each use makes a fresh pair and puts it on the target.
/// The tool itself never leaves the cyborg.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgCuffDispenserComponent : Component
{
    [DataField]
    public EntProtoId Cuffs = "Zipties";

    [DataField]
    public TimeSpan CuffTime = TimeSpan.FromSeconds(3.5);

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/ziptie_start.ogg");

    [DataField]
    public SoundSpecifier EndSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/ziptie_end.ogg");
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// Prints items where the cyborg clicks, paid from its power cell.
/// The port of tgstation's RSF and cookie synthesizer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyborgDispenserComponent : Component
{
    /// <summary>
    /// What can be printed. Using the tool in hand cycles through them.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Options = new();

    [DataField, AutoNetworkedField]
    public int Selected;

    [DataField]
    public float ChargeCost = 20f;

    [DataField]
    public SoundSpecifier? DispenseSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
}

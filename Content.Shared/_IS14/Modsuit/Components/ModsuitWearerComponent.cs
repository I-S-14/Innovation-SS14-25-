// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     Sits on whoever is wearing a MOD suit.
///
///     Exists so tools can be used on the person rather than on the suit. A suit worn on
///     somebody's back cannot be clicked: the click lands on the mob, and the mob has no
///     idea it is wearing anything. This marker is what gives the suit an ear on the body
///     it is bolted to, and it is the whole reason getting somebody out of a MOD is
///     possible at all.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModsuitWearerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Suit;
}

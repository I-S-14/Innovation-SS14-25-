// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's cyborg rename board: write a name with it in hand, then install it to give the cyborg that name.
/// Used up on install.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyborgRenameBoardComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? NewName;

    [DataField]
    public int MaxLength = 32;
}

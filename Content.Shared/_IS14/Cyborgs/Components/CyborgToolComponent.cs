// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// Marks an item as a built-in cyborg tool. It lives in the cyborg's tool storage,
/// can only be held in a tool slot and can't be dropped or handed over.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CyborgToolComponent : Component
{
    /// <summary>
    /// The cyborg this tool belongs to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Cyborg;

    /// <summary>
    /// Tools from the model's emag list stay hidden until the cyborg is emagged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool EmagTool;

    /// <summary>
    /// The upgrade board that gave this tool, so removing the board takes it away again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SourceUpgrade;
}

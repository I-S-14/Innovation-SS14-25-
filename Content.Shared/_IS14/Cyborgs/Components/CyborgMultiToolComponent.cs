// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// A cyborg tool that cycles through modes on use, each mode bringing its own components.
/// Unlike the upstream multiple tool it swaps whole components, so it can carry tools
/// that are defined by components rather than tool qualities, like Shitmed's surgery tools.
/// The port of tgstation's medical omnitool.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CyborgMultiToolComponent : Component
{
    [DataField(required: true)]
    public List<CyborgToolMode> Modes = new();

    [DataField, AutoNetworkedField]
    public int Current;
}

[DataDefinition]
public sealed partial class CyborgToolMode
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// State of the tool's first sprite layer in this mode.
    /// </summary>
    [DataField(required: true)]
    public string State = string.Empty;

    [DataField]
    public ComponentRegistry Components = new();
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// A fill gauge the cyborg HUD draws over a tool's slot, like tg's welder fuel overlay.
/// It is only on the HUD: the tool's own sprite is left alone.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgHudGaugeComponent : Component
{
    /// <summary>
    /// Solution whose fill the gauge shows.
    /// </summary>
    [DataField(required: true)]
    public string Solution = string.Empty;

    [DataField(required: true)]
    public ResPath Sprite;

    /// <summary>
    /// Gauge states from empty to full. The first one is only for an empty solution, the rest split the
    /// fill evenly with each one covering the band below it: with five states the full one shows above 75%.
    /// </summary>
    [DataField(required: true)]
    public List<string> States = new();
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// Draws the tool in the cyborg's tool grid with its own icon instead of its sprite, like the janitor's
/// cleaner box: the box sits in the grid while the vacuum wand is what the tool slot shows.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgHudIconComponent : Component
{
    [DataField(required: true)]
    public ResPath Sprite;

    [DataField(required: true)]
    public string State = string.Empty;

    /// <summary>
    /// Indicator drawn over the icon while the tool's item toggle is on.
    /// </summary>
    [DataField]
    public string? ActiveOverlay;

    /// <summary>
    /// Indicator drawn over the icon while the tool's item toggle is off.
    /// </summary>
    [DataField]
    public string? InactiveOverlay;

    /// <summary>
    /// Drawn over the icon while the tool is stowed, like the wand folded onto the box.
    /// </summary>
    [DataField]
    public string? StowedOverlay;
}

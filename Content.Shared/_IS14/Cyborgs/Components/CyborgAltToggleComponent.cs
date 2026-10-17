// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// Moves an item toggle to the alternative interaction, so a click still does the item's main job.
/// The cyborg's ore and construction bags open on a click and switch their magnet on an alt click.
/// Pair it with an ItemToggle that has onUse and onActivate turned off.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgAltToggleComponent : Component;

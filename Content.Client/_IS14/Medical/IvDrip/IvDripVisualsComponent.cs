// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.IvDrip;

namespace Content.Client._IS14.Medical.IvDrip;

/// <summary>
/// Which RSI states a drip stand draws itself with. In the prototype rather than in code
/// so a resprite is a YAML edit, and so a stand cut on a different sheet can reuse the
/// visualizer without inheriting somebody else's state names.
/// </summary>
[RegisterComponent]
public sealed partial class IvDripVisualsComponent : Component
{
    /// <summary>
    /// One whole-pole state per mood. The base layer is swapped outright rather than
    /// stacked with overlays: the running frames differ from the idle ones down the
    /// length of the stand, not in one corner of it.
    /// </summary>
    [DataField]
    public Dictionary<IvDripVisualState, string> States = new()
    {
        [IvDripVisualState.Idle] = "iv_drip",
        [IvDripVisualState.InjectIdle] = "iv_drip_injectidle",
        [IvDripVisualState.Injecting] = "iv_drip_injecting",
        [IvDripVisualState.DrawIdle] = "iv_drip_donateidle",
        [IvDripVisualState.Drawing] = "iv_drip_donating",
    };

    /// <summary>The hanging bag, still and running.</summary>
    [DataField]
    public string BeakerIdleState = "beakeridle";

    [DataField]
    public string BeakerActiveState = "beakeractive";

    /// <summary>
    /// The bag's gauge, emptiest first. Indexed by the step the server already worked
    /// out, so the order here has to match the stand's own fill thresholds.
    /// </summary>
    [DataField]
    public List<string> ReagentStates = new()
    {
        "reagent0",
        "reagent10",
        "reagent25",
        "reagent50",
        "reagent75",
        "reagent80",
        "reagent90",
    };
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Overlays;

/// <summary>
///     Redraws the station's layout with its own sprites inside the area the field of view
///     has blacked out, so it stays readable through obstructions. Floor tiles, plus every
///     entity carrying <see cref="StructuralVisionTargetComponent"/> — walls, windows and
///     grilles as shipped.
///
///     Nothing here reveals anything the client did not already hold. PVS is a radius, not
///     a line of sight (net.pvs_range, 25m by default), so the wall in the next room is
///     already in memory and is only hidden at draw time by the FOV mask. This declines to
///     respect that mask, for the layout and nothing else.
///
///     Deliberately blind to anything that moves: no mobs, no items, no machines. That is
///     what keeps it a map rather than a wallhack — it answers "where does this corridor
///     go", never "who is standing round the corner".
/// </summary>
// raiseAfterAutoHandleState: the client system refreshes the overlay when new settings
// land, which needs the event the generator only emits when asked.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StructuralVisionComponent : Component
{
    /// <summary>
    ///     Reach of the scan, in metres. Past the PVS radius there is simply nothing on the
    ///     client to draw, so raising this beyond it buys an invisible nothing. The default
    ///     covers a normal viewport outright, which keeps the cutoff off-screen instead of
    ///     drawing a circle the wearer can see the edge of.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 20f;

    /// <summary>
    ///     Multiplied into everything the scanner redraws.
    ///
    ///     Two jobs. It knocks the brightness down: the redraw is unlit, so at full strength
    ///     it comes out brighter than the lit room next to it and the seam along the edge of
    ///     the field of view turns into a hard line. And it warms the result — station
    ///     plating and walls are cold grey to begin with, and unlit they read as flatly blue.
    ///
    ///     Per channel, so raising all three together only changes brightness. Dropping blue
    ///     relative to red is what takes the chill off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color Tint = new Color(0.62f, 0.59f, 0.52f);

    /// <summary>
    ///     Width of the fade along the edge of the field of view, in screen pixels. Zero
    ///     turns the extra pass off and leaves the hard edge.
    ///
    ///     The redraw is clipped by the stencil the FOV pass writes, which is binary, so
    ///     without this it simply stops dead along the line where the field of view ends.
    ///     Screen pixels rather than metres because that is the unit the seam is actually
    ///     read in — a fixed number of them looks the same at any zoom.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Feather = 24f;
}

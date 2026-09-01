// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Overlays;

/// <summary>
///     Draws the station's structure — deck plating, walls, doors — on top of the field of
///     view, so it stays readable through obstructions.
///
///     Nothing here reveals anything the client did not already hold. PVS is a radius, not
///     a line of sight (net.pvs_range, 25m by default), so the wall in the next room is
///     already in memory and is only hidden at draw time by the FOV mask. This declines to
///     respect that mask, for three things and no others.
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
    ///     client to draw, so raising this beyond it buys an invisible nothing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 14f;

    /// <summary>
    ///     Deck plating. Faint on purpose: it covers every tile on screen, including the
    ///     room the wearer is standing in, and has to tint that room rather than hide it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color FloorColor = new Color(0.12f, 0.43f, 0.29f, 0.16f);

    /// <summary>
    ///     Anything that stops light: walls, windows, closed shutters. Read straight off the
    ///     occluder, so it needs no tags and no edits to upstream prototypes — if the engine
    ///     thinks it blocks sight, the scanner draws it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color WallColor = new Color(0.25f, 0.82f, 0.54f, 0.5f);

    /// <summary>
    ///     Doors, drawn open or shut alike. An open door stops occluding and would vanish
    ///     from the wall pass — but a door is the one thing you are actually looking for on
    ///     a floor plan, so it gets its own pass and its own colour.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color DoorColor = new Color(0.49f, 0.88f, 1f, 0.6f);
}

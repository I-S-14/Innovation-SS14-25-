// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Overlays;

/// <summary>
///     Marks an entity as part of the station's layout, so <see cref="StructuralVisionComponent"/>
///     draws it through walls. This is the entire list — the scanner draws floor tiles, and
///     it draws entities carrying this. Nothing else.
///
///     It sits on six upstream base prototypes and everything inherits from there:
///     BaseStructureWall (all walls, asteroid rock, meteors), Window, WindowDirectional and
///     PlastitaniumWindowBase (all windows), Grille and GrilleBroken.
///
///     It used to be a guess instead — ask the engine which entities block light and draw
///     those. That is wrong in both directions. It catches bookshelves, curtains and closed
///     airlocks, none of which are layout, and it cannot see a grille at all, because a
///     grille blocks nothing.
///
///     Mind what this is for. Everything listed here is visible through walls to anyone
///     wearing the goggles, so it belongs on things that answer "where does this corridor
///     go". Putting it on anything that moves, or anything that tells you what a room is
///     being used for right now, turns a floor plan into a wallhack.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StructuralVisionTargetComponent : Component;

// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     Marks a module as putting a hole in the faceplate: an iris the wearer can push a
///     straw or a ration bar through without breaking the seal.
///
///     Lives in the suit layer rather than the chassis one on purpose — a mech has no
///     face, and the generic chassis has no business knowing what a helmet is.
///     <see cref="Systems.SharedModsuitSystem"/> reads this off installed modules.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleFaceOpeningComponent : Component;

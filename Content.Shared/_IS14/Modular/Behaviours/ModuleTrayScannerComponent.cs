// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Turns the chassis itself into a t-ray scanner while the module is running.
///
///     Granting the scanner component to the wearer looks right and does nothing: the
///     client sweeps the player's inventory slots and hands for a scanner, and a person is
///     not an item in their own inventory. The chassis is — it is worn — so the scanner
///     goes there. The eye mask that lets the subfloor render at all is a second, separate
///     thing, and is the other half of why this module drew a blank.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleTrayScannerComponent : Component
{
    /// <summary>
    ///     Tiles swept around the wearer.
    /// </summary>
    [DataField]
    public float Range = 5f;

    /// <summary>
    ///     Who currently holds the subfloor vis mask on our account, so it comes back off
    ///     the right person if the suit changes hands mid-sweep.
    /// </summary>
    [ViewVariables]
    public EntityUid? Viewer;

    /// <summary>
    ///     Whether the scanner is currently bolted to the chassis.
    /// </summary>
    [ViewVariables]
    public EntityUid? Scanning;
}

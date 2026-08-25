// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Clamps the operator to the floor while the module runs, reusing the station's own
///     magboot machinery so the wearer gets the same status alert, the same immunity to
///     space wind and the same weightlessness handling as a pair of real magboots.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleMagbootsComponent : Component
{
    /// <summary>
    ///     Who the effect is currently applied to, so it comes off the right person when
    ///     the suit changes hands.
    /// </summary>
    [ViewVariables]
    public EntityUid? AppliedTo;

    /// <summary>
    ///     Whether we were the ones who put the magboot component on them. Someone already
    ///     wearing real magboots keeps theirs when the module shuts down.
    /// </summary>
    [ViewVariables]
    public bool Granted;
}

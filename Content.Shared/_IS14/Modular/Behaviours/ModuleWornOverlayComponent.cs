// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Draws a module's own art on top of the suit part it lives in, so a suit with a
///     drill and a suit with a flashlight do not look identical from across the room.
///
///     Rendering happens client-side: <c>ClothingComponent.ClothingVisuals</c> is not
///     networked, so the layers are contributed at draw time from the module's networked
///     state rather than pushed from the server.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleWornOverlayComponent : Component
{
    /// <summary>
    ///     Sheet the overlay states come from.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ResPath Rsi;

    /// <summary>
    ///     Part this overlay draws on. Must match one of the suit's parts, and the part
    ///     has to be deployed for anything to show.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags TargetSlot = SlotFlags.NONE;

    /// <summary>
    ///     Shown while the module is installed and runnable but switched off.
    ///     Null means nothing is drawn in that state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? StateInactive;

    /// <summary>
    ///     Shown while the module is switched on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? StateActive;

    /// <summary>
    ///     Shown while the module is on cooldown, taking priority over the other two.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? StateCooldown;

    /// <summary>
    ///     Draw ignoring lighting — for anything that glows.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Unshaded;

    /// <summary>
    ///     Only draw once the part is sealed, rather than merely deployed. Use for
    ///     overlays that only make sense on a closed-up suit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequireSealed;
}

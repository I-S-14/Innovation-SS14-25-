// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Where a behaviour puts the components it grants.
/// </summary>
[Serializable, NetSerializable]
public enum ModuleGrantTarget : byte
{
    /// <summary>The person wearing or piloting the chassis.</summary>
    Wearer = 0,

    /// <summary>The chassis entity itself.</summary>
    Chassis = 1,

    /// <summary>The module entity — useful for self-contained gear.</summary>
    Self = 2,
}

/// <summary>
///     Adds a set of components to a target while the module is running, and takes them
///     away again when it stops.
///
///     This one behaviour covers most of the catalogue: every visor and HUD, night vision,
///     radiation shielding, anti-slip, thermal regulation, welding protection, plasma
///     stabilisation, longfall. Those modules are pure YAML because of it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleGrantComponentsComponent : Component
{
    /// <summary>
    ///     Who receives the components.
    /// </summary>
    [DataField]
    public ModuleGrantTarget Target = ModuleGrantTarget.Wearer;

    /// <summary>
    ///     Components granted while the module is running.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    /// <summary>
    ///     When true the grant follows the module being switched on rather than merely
    ///     being installed and supplied. Toggleable modules want this; passive ones do not.
    /// </summary>
    [DataField]
    public bool RequireActive;

    /// <summary>
    ///     Entity the components were last granted to, so they can be taken back off the
    ///     right one even if the wearer changed in between.
    /// </summary>
    [ViewVariables]
    public EntityUid? GrantedTo;

    /// <summary>
    ///     Registry keys this module actually added. A component the target already had
    ///     of its own — NoSlip from their shoes, say — is left alone on both grant and
    ///     revoke, so a module never strips gear it did not provide.
    /// </summary>
    [ViewVariables]
    public List<string> GrantedKeys = new();
}

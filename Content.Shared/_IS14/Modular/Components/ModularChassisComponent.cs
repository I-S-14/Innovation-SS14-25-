// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modular.Components;

/// <summary>
///     Hosts <see cref="ChassisModuleComponent"/> modules and enforces a complexity budget on them.
///     Knows nothing about clothing, inventory slots or wearers — a MOD control unit and a mech
///     are both just chassis. Anything that needs to know about being worn belongs on the
///     modsuit layer instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedModularChassisSystem), typeof(SharedChassisModuleSystem))]
public sealed partial class ModularChassisComponent : Component
{
    /// <summary>
    ///     Container holding every installed module.
    /// </summary>
    [DataField]
    public string ModuleContainerId = "chassis-modules";

    /// <summary>
    ///     Populated on init, but null until then — resolve it through the system
    ///     rather than reading it directly.
    /// </summary>
    [ViewVariables]
    public Container? ModuleContainer;

    /// <summary>
    ///     How much module complexity this chassis can carry.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxComplexity = 15;

    /// <summary>
    ///     Sum of <see cref="ChassisModuleComponent.Complexity"/> over installed modules.
    ///     Recalculated on every install/uninstall, never edited directly.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int UsedComplexity;

    /// <summary>
    ///     Whether the chassis is switched on. Modules only run while this is true,
    ///     unless they carry <see cref="ModuleAllowFlags.ChassisInactive"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    ///     The single <see cref="ModuleKind.Active"/> module currently selected, if any.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? SelectedModule;

    /// <summary>
    ///     Set while the hardware panel is open. Installing and removing modules requires it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PanelOpen;

    /// <summary>
    ///     Modules spawned into the chassis on map init. Intended for built-in, unremovable
    ///     modules that cost no complexity — a theme's signature gear.
    /// </summary>
    [DataField]
    public List<EntProtoId> IntegratedModules = new();

    /// <summary>
    ///     Set once <see cref="IntegratedModules"/> have been spawned so they are not duplicated.
    /// </summary>
    [DataField]
    public bool IntegratedModulesSpawned;

    #region Feedback

    [DataField]
    public SoundSpecifier InstallSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/module_click.ogg");

    [DataField]
    public SoundSpecifier RemoveSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/module_click.ogg");

    [DataField]
    public SoundSpecifier FailSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/fail.ogg");

    #endregion
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modular.Components;

/// <summary>
///     An item that can be installed into a <see cref="ModularChassisComponent"/>.
///     This component carries only bookkeeping — cost, power, requirements, state.
///     What the module actually *does* lives in separate behaviour components on the
///     same entity, which react to the module events.
/// </summary>
// raiseAfterAutoHandleState: the client has no other notice that a module was
// switched — the toggle is a server-side interface message — and the worn overlay
// has to be repainted when it lands.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
// Outside systems and the UI legitimately read and enumerate these; only the two
// owning systems may write them.
[Access(typeof(SharedChassisModuleSystem), typeof(SharedModularChassisSystem),
    Other = AccessPermissions.ReadExecute)]
public sealed partial class ChassisModuleComponent : Component
{
    /// <summary>
    ///     How this module is operated. See <see cref="ModuleKind"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ModuleKind Kind = ModuleKind.Passive;

    /// <summary>
    ///     Cost against the chassis' complexity budget.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Complexity = 1;

    /// <summary>
    ///     Constant draw in watts while installed, whether on or off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float IdleDraw;

    /// <summary>
    ///     Draw in watts while switched on. Only meaningful for
    ///     <see cref="ModuleKind.Toggleable"/> and <see cref="ModuleKind.Active"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ActiveDraw;

    /// <summary>
    ///     Joules spent per activation of a <see cref="ModuleKind.Usable"/> module,
    ///     or per special click of an <see cref="ModuleKind.Active"/> one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UseCost;

    /// <summary>
    ///     Cooldown applied after each use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.Zero;

    /// <summary>
    ///     Game time at which the cooldown expires.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CooldownEnd = TimeSpan.Zero;

    /// <summary>
    ///     False for modules built into a chassis at construction. Those should also
    ///     carry <see cref="Complexity"/> 0 so they never eat the player's budget.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Removable = true;

    /// <summary>
    ///     Slots the host must provide for this module to run, e.g. a visor needs a helmet.
    ///     Every entry must be satisfied; flags combined inside one entry are alternatives.
    ///     So <c>[HEAD, GLOVES|FEET]</c> means "a head part AND (gloves OR boots)".
    ///     Empty means the module works regardless of which parts are deployed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<SlotFlags> RequiredSlots = new();

    /// <summary>
    ///     Tags this module refuses to coexist with. Checked in both directions against
    ///     every installed module, so tagging all visors <c>ModsuitVisor</c> makes them
    ///     mutually exclusive without any of them naming the others.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> Conflicts = new();

    /// <summary>
    ///     Extra circumstances this module tolerates. See <see cref="ModuleAllowFlags"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ModuleAllowFlags Allow = ModuleAllowFlags.None;

    /// <summary>
    ///     Locale id for this module's button in the interface, when the generic wording
    ///     for its kind would be wrong — a storage module is "open", not "use".
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? ActionText;

    /// <summary>
    ///     Texture path for that button's icon, overriding the one picked from the kind.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? ActionIcon;

    /// <summary>
    ///     Played when the module switches on. Sound belongs to the module rather than to
    ///     its behaviour so a flashlight can click without the light system knowing what
    ///     a click is — and so any other toggle can have one from YAML alone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? ActivateSound;

    /// <summary>
    ///     Played when it switches off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? DeactivateSound;

    /// <summary>
    ///     Chassis this module is installed in, if any.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Chassis;

    /// <summary>
    ///     True while the module is switched on.
    ///     Passive modules mirror <see cref="Enabled"/> here.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Active;

    /// <summary>
    ///     True while the module's slot requirements are met and the chassis can run it.
    ///     Behaviours hook <c>ModuleEnabledEvent</c>/<c>ModuleDisabledEvent</c> rather than
    ///     polling this.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    ///     Users who pinned this module to their action bar.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> PinnedActions = new();
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared.Alert;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     The MOD control unit — the backpack-slot entity that is the suit.
///     Everything else (parts, modules, core) lives inside it. Sits on top of
///     <c>ModularChassisComponent</c>, which handles the module side of things.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedModsuitSystem))]
public sealed partial class ModsuitControlComponent : Component
{
    /// <summary>
    ///     Container holding the suit parts while they are folded away.
    /// </summary>
    [DataField]
    public string PartContainerId = "modsuit-parts";

    /// <summary>
    ///     Populated on init, but null until then — resolve it through the system
    ///     rather than reading it directly.
    /// </summary>
    [ViewVariables]
    public Container? PartContainer;

    /// <summary>
    ///     Parts to spawn on map init, keyed by the inventory slot they deploy into.
    ///     Not a fixed set of four: a light suit can be just a helmet and chestplate,
    ///     and modules ask for slots rather than for named parts.
    /// </summary>
    [DataField]
    public Dictionary<string, EntProtoId> PartPrototypes = new();

    /// <summary>
    ///     Live parts, keyed by inventory slot name.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, EntityUid> Parts = new();

    [DataField]
    public bool PartsSpawned;

    /// <summary>
    ///     Who is currently wearing the suit, if anyone.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Wearer;

    /// <summary>
    ///     Inventory slot the control unit itself must occupy for the suit to work.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlotFlags RequiredSlot = SlotFlags.BACK;

    /// <summary>
    ///     Time taken to seal or unseal a single part. Sealing the whole suit walks
    ///     the parts one at a time, so this is the per-step delay, not the total.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SealTimePerPart = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     True while a seal or unseal sequence is running. Blocks most other interactions.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Sealing;

    /// <summary>
    ///     Parts still waiting to blow open after the suit lost power. Server-side: this
    ///     is not a player action and there is nothing for the client to predict.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> BlowoutQueue = new();

    /// <summary>
    ///     When the next part in <see cref="BlowoutQueue"/> gives way.
    /// </summary>
    [ViewVariables]
    public TimeSpan? BlowoutNext;

    /// <summary>
    ///     Gap between parts blowing open. Shorter than a deliberate unseal — the suit is
    ///     failing, not being operated — but not instant, because a suit coming apart
    ///     around somebody should be heard happening.
    /// </summary>
    [DataField]
    public TimeSpan BlowoutInterval = TimeSpan.FromSeconds(0.6);

    /// <summary>
    ///     Parts still waiting to be sealed or unsealed in the current sequence.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> SealQueue = new();

    /// <summary>
    ///     Direction of the running sequence: true while sealing up, false while unsealing.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool SealingUp;

    /// <summary>
    ///     Movement penalty applied while the suit is sealed, split across its parts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SealedSlowdown = 0.75f;

    /// <summary>
    ///     Delay for another player operating the suit through the stripping menu.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StripDelay = TimeSpan.FromSeconds(10);

    #region Feedback

    /// <summary>
    ///     Charge readout shown to the wearer in the alert strip.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> ChargeAlert = "IS14ModsuitCharge";

    /// <summary>
    ///     Shown instead when the suit has no core, or a core with nothing left in it.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> NoChargeAlert = "IS14ModsuitChargeNone";


    [DataField]
    public SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/part_move.ogg");

    [DataField]
    public SoundSpecifier RetractSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/part_move.ogg");

    [DataField]
    public SoundSpecifier SealCompleteSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/seal_complete.ogg");

    [DataField]
    public SoundSpecifier FailSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/fail.ogg");

    #endregion
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     One piece of a MOD suit — helmet, chestplate, gauntlets, boots or anything else
///     a theme cares to define. Parts are real clothing entities so they can carry their
///     own armour, sprites and storage; the control unit just moves them in and out of
///     the wearer's inventory.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedModsuitSystem))]
public sealed partial class ModsuitPartComponent : Component
{
    /// <summary>
    ///     Control unit this part belongs to.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Control;

    /// <summary>
    ///     Inventory slot this part deploys into, e.g. "head" or "outerClothing".
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string Slot = string.Empty;

    /// <summary>
    ///     Slot flag this part contributes when deployed. Modules match their
    ///     requirements against the union of these.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags SlotFlag = SlotFlags.NONE;

    /// <summary>
    ///     True while the part is worn rather than folded into the control unit.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Deployed;

    /// <summary>
    ///     True once the part is pressure-sealed. Only sealed parts count towards the
    ///     slots offered to modules, and only sealed parts protect against vacuum.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Sealed;

    /// <summary>
    ///     Components granted to the part while it is sealed — pressure and temperature
    ///     protection, and anything else a theme wants to add. Applied by the seal system
    ///     rather than being always-on, which is what makes "deployed but unsealed"
    ///     a genuinely different state.
    /// </summary>
    [DataField]
    public ComponentRegistry SealedComponents = new();

    /// <summary>
    ///     Popup shown to the wearer as this part seals.
    /// </summary>
    [DataField]
    public LocId? SealPopup;

    /// <summary>
    ///     Popup shown to the wearer as this part unseals.
    /// </summary>
    [DataField]
    public LocId? UnsealPopup;

    #region Integrity

    /// <summary>
    ///     How much punishment this piece of plating takes before it stops being
    ///     structurally useful. Set per part: a chestplate is built heavier than a glove.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxIntegrity = 200f;

    /// <summary>
    ///     Condition left, seeded from <see cref="MaxIntegrity"/> when the part comes up.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float Integrity;

    /// <summary>
    ///     Fraction of <see cref="MaxIntegrity"/> at or below which the piece stops
    ///     offering its slot to modules. The suit still seals — a dented gauntlet is
    ///     airtight long after the hardpoint inside it has stopped answering.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ModuleThreshold = 0.66f;

    /// <summary>
    ///     Fraction at which the piece can no longer hold pressure at all: it pops open
    ///     on its own and refuses to seal again until it has been worked on. This is the
    ///     line that turns a bad fight into an emergency rather than an inconvenience.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UnsealThreshold = 0.33f;

    /// <summary>
    ///     Body parts whose damage lands on this piece. Any overlap counts, so a
    ///     chestplate covering the chest and the groin takes hits to either.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<TargetBodyPart> CoveredParts = new();

    /// <summary>
    ///     Per damage type multipliers on what the plating takes. Ion is doubled by
    ///     default: a suit is exactly the sort of thing an ion weapon is pointed at.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> DamageMultipliers = new()
    {
        ["Ion"] = 2f,
    };

    /// <summary>
    ///     Damage types that dent plating rather than cook the wiring behind it.
    ///     Anything not listed here is treated as an electrical fault.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> StructuralDamage = new()
    {
        "Blunt",
        "Piercing",
        "Slash",
        "Structural",
    };

    /// <summary>
    ///     Running totals of what has been done to this piece. Only the ratio matters —
    ///     they decide which repair the piece is asking for, not how much of it it needs.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float StructuralWear;

    [ViewVariables, AutoNetworkedField]
    public float ElectricalWear;

    /// <summary>
    ///     Share of <see cref="MaxIntegrity"/> one round of repair puts back.
    /// </summary>
    [DataField]
    public float RepairFraction = 0.25f;

    /// <summary>
    ///     Time one round of repair takes.
    /// </summary>
    [DataField]
    public TimeSpan RepairDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    ///     Welder fuel one round of plate work burns.
    /// </summary>
    [DataField]
    public float RepairFuel = 5f;

    #endregion

    #region Overslotting

    /// <summary>
    ///     Whether this part may deploy over clothing the wearer already has on,
    ///     stowing it until the part is retracted. Without this a pair of gloves is
    ///     enough to stop the suit closing, which is not the fantasy.
    /// </summary>
    [DataField]
    public bool CanOverslot = true;

    /// <summary>
    ///     Container holding the garment this part displaced.
    /// </summary>
    [DataField]
    public string OverslotContainerId = "modsuit-overslot";

    [ViewVariables]
    public ContainerSlot? OverslotContainer;

    #endregion

    #region Feedback

    /// <summary>
    ///     The plating moving as the part pressurises.
    /// </summary>
    [DataField]
    public SoundSpecifier SealSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/part_move.ogg");

    /// <summary>
    ///     And moving back as it opens up again.
    /// </summary>
    [DataField]
    public SoundSpecifier UnsealSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/part_move.ogg");

    #endregion
}

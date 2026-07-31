using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Economy.VendingMachine;

[RegisterComponent]
public sealed partial class IS14VendingMachineComponent : Component
{
    /// <summary>Optional display-name override for the UI; empty — the entity name is used.</summary>
    [DataField]
    public string MachineName = string.Empty;

    /// <summary>
    /// Station account (department fund) that receives this machine's revenue.
    /// Null — revenue goes entirely to the treasury.
    /// </summary>
    [DataField]
    public ProtoId<StationAccountPrototype>? RevenueAccount;

    /// <summary>
    /// Fraction of each sale sent to the station treasury as tax.
    /// The rest goes to <see cref="RevenueAccount"/>.
    /// </summary>
    [DataField]
    public float TaxPercent = 0.1f;

    /// <summary>Whether cash can be fed into the machine and spent instead of a bank account.</summary>
    [DataField]
    public bool AcceptsCash = true;

    /// <summary>Stack prototype accepted as cash.</summary>
    [DataField]
    public ProtoId<StackPrototype> CashStackType = "Credit";

    /// <summary>
    /// Cash currently sitting in the machine. While this is above zero the machine sells for
    /// cash and no buyer account is involved at all. Returned through the UI button.
    /// </summary>
    [ViewVariables]
    public int InsertedCash;

    /// <summary>Who fed the cash in, so an abandoned session gives it back to them and not to the next customer.</summary>
    [ViewVariables]
    public EntityUid? CashOwner;

    [DataField]
    public List<VendingMachineTab> Tabs = new();

    [DataField]
    public List<VendingMachineEntry> ContrabandInventory = new();

    [DataField]
    public List<LocId> AdMessages = new();

    public bool ContrabandUnlocked;

    [DataField]
    public SoundSpecifier BuySound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    [DataField]
    public SoundSpecifier InsertCashSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    [DataField]
    public SoundSpecifier EjectCashSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    // ── Sprite states (vanilla vending machine conventions) ──────────────────

    /// <summary>Base layer state while unbroken.</summary>
    [DataField]
    public string OffState = "off";

    /// <summary>Base layer state when the machine is broken.</summary>
    [DataField]
    public string BrokenState = "broken";

    /// <summary>Unshaded screen layer state while powered.</summary>
    [DataField]
    public string NormalState = "normal-unshaded";

    /// <summary>Unshaded screen layer state while denying a purchase.</summary>
    [DataField]
    public string DenyState = "deny-unshaded";

    /// <summary>How long the deny screen state is shown.</summary>
    [DataField]
    public TimeSpan DenyDelay = TimeSpan.FromSeconds(2.0);

    /// <summary>Smashed by damage; repaired with a welder.</summary>
    [ViewVariables]
    public bool Broken;

    /// <summary>Server: game time when the current deny animation ends.</summary>
    [ViewVariables]
    public TimeSpan DenyEnd;
}

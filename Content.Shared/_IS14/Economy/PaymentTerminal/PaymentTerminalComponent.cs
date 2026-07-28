using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Economy.PaymentTerminal;

/// <summary>
/// P2P payment terminal: the owner sets a charge, the customer pays it with their ID card.
/// Revenue goes to a station fund (if configured) or to a bound personal account.
/// </summary>
[RegisterComponent]
public sealed partial class IS14PaymentTerminalComponent : Component
{
    /// <summary>Station fund receiving payments. Null — payments go to the bound personal account.</summary>
    [DataField]
    public ProtoId<StationAccountPrototype>? RevenueAccount;

    /// <summary>Personal account payments go to when no fund is configured. Bound via the UI.</summary>
    [DataField]
    public int? RecipientAccountNumber;

    /// <summary>Card holder name shown in the UI for a personal recipient.</summary>
    [DataField]
    public string RecipientName = string.Empty;

    /// <summary>Fraction of every payment sent to the treasury as tax.</summary>
    [DataField]
    public float TaxPercent = 0.1f;

    /// <summary>Currently requested payment; 0 — nothing to pay.</summary>
    [DataField]
    public int PendingAmount;

    /// <summary>Optional note shown to the payer and written to the transaction log.</summary>
    [DataField]
    public string PendingDescription = string.Empty;

    /// <summary>Receipt paper spawned after a successful payment. Null — no receipt.</summary>
    [DataField]
    public EntProtoId? ReceiptPrototype = "IS14PaymentReceipt";

    [DataField]
    public SoundSpecifier PaySound = new SoundPathSpecifier("/Audio/Items/appraiser.ogg");

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
}

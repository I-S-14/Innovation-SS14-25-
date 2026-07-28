using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.PaymentTerminal;

[Serializable, NetSerializable]
public sealed class IS14PaymentTerminalUiState : BoundUserInterfaceState
{
    /// <summary>Localized recipient line («Получатель: …» / «Получатель не привязан»).</summary>
    public readonly string RecipientLabel;

    /// <summary>True when the terminal has somewhere to send money.</summary>
    public readonly bool HasRecipient;

    /// <summary>True when the recipient is a personal card that can be (re)bound via the UI.</summary>
    public readonly bool CanBind;

    /// <summary>Requested payment; 0 — the charge entry screen is shown instead.</summary>
    public readonly int PendingAmount;

    public readonly string PendingDescription;

    /// <summary>Localized status line from the last operation (empty — nothing to show).</summary>
    public readonly string Status;

    public IS14PaymentTerminalUiState(string recipientLabel, bool hasRecipient, bool canBind,
        int pendingAmount, string pendingDescription, string status = "")
    {
        RecipientLabel = recipientLabel;
        HasRecipient = hasRecipient;
        CanBind = canBind;
        PendingAmount = pendingAmount;
        PendingDescription = pendingDescription;
        Status = status;
    }
}

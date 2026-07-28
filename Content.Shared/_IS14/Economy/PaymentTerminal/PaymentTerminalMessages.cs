using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.PaymentTerminal;

/// <summary>Owner request: set the current charge.</summary>
[Serializable, NetSerializable]
public sealed class IS14PaymentTerminalSetChargeMessage : BoundUserInterfaceMessage
{
    public readonly int Amount;
    public readonly string Description;

    public IS14PaymentTerminalSetChargeMessage(int amount, string description)
    {
        Amount = amount;
        Description = description;
    }
}

/// <summary>Owner request: cancel the current charge.</summary>
[Serializable, NetSerializable]
public sealed class IS14PaymentTerminalCancelMessage : BoundUserInterfaceMessage
{
}

/// <summary>Customer request: pay the current charge with the actor's ID card.</summary>
[Serializable, NetSerializable]
public sealed class IS14PaymentTerminalPayMessage : BoundUserInterfaceMessage
{
}

/// <summary>Bind the actor's ID card account as the payment recipient.</summary>
[Serializable, NetSerializable]
public sealed class IS14PaymentTerminalBindCardMessage : BoundUserInterfaceMessage
{
}

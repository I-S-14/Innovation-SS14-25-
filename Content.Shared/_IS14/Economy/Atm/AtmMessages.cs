using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Atm;

/// <summary>Sets the PIN on first login (SetPin screen).</summary>
[Serializable, NetSerializable]
public sealed class IS14AtmSetPinMessage : BoundUserInterfaceMessage
{
    public readonly string Pin;

    public IS14AtmSetPinMessage(string pin)
    {
        Pin = pin;
    }
}

/// <summary>PIN entry attempt (EnterPin screen).</summary>
[Serializable, NetSerializable]
public sealed class IS14AtmEnterPinMessage : BoundUserInterfaceMessage
{
    public readonly string Pin;

    public IS14AtmEnterPinMessage(string pin)
    {
        Pin = pin;
    }
}

/// <summary>Changes the PIN of the authenticated session's account.</summary>
[Serializable, NetSerializable]
public sealed class IS14AtmChangePinMessage : BoundUserInterfaceMessage
{
    public readonly string Pin;

    public IS14AtmChangePinMessage(string pin)
    {
        Pin = pin;
    }
}

/// <summary>Withdraws cash from the authenticated account.</summary>
[Serializable, NetSerializable]
public sealed class IS14AtmWithdrawMessage : BoundUserInterfaceMessage
{
    public readonly int Amount;

    public IS14AtmWithdrawMessage(int amount)
    {
        Amount = amount;
    }
}

/// <summary>Transfers credits from the authenticated account to another account.</summary>
[Serializable, NetSerializable]
public sealed class IS14AtmTransferMessage : BoundUserInterfaceMessage
{
    public readonly int TargetAccount;
    public readonly int Amount;

    public IS14AtmTransferMessage(int targetAccount, int amount)
    {
        TargetAccount = targetAccount;
        Amount = amount;
    }
}

/// <summary>Pays one of the cardholder's outstanding fines from the authenticated account.</summary>
[Serializable, NetSerializable]
public sealed class IS14AtmPayFineMessage : BoundUserInterfaceMessage
{
    public readonly uint FineId;

    public IS14AtmPayFineMessage(uint fineId)
    {
        FineId = fineId;
    }
}

/// <summary>Ejects the inserted ID card and ends the session.</summary>
[Serializable, NetSerializable]
public sealed class IS14AtmEjectCardMessage : BoundUserInterfaceMessage
{
}

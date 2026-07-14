using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Atm;

[Serializable, NetSerializable]
public enum IS14AtmScreen : byte
{
    /// <summary>No card inserted (or the card has no bank account).</summary>
    NoCard,
    /// <summary>First login: the owner is asked to pick a PIN.</summary>
    SetPin,
    /// <summary>PIN entry required.</summary>
    EnterPin,
    /// <summary>Account locked after too many failed PIN entries.</summary>
    Locked,
    /// <summary>Authenticated: operations available.</summary>
    Menu,
}

[Serializable, NetSerializable]
public sealed class IS14AtmUiState : BoundUserInterfaceState
{
    public readonly IS14AtmScreen Screen;
    /// <summary>Full name on the inserted ID card, empty if none.</summary>
    public readonly string CardOwner;
    /// <summary>The inserted ID card entity; used to render its sprite in the UI.</summary>
    public readonly NetEntity? IdCard;
    /// <summary>Account number of the inserted card, 0 if none.</summary>
    public readonly int AccountNumber;
    public readonly int Balance;
    /// <summary>PIN attempts left before the account is locked (EnterPin screen).</summary>
    public readonly int AttemptsLeft;
    /// <summary>Seconds until the account unlocks (Locked screen).</summary>
    public readonly int LockSecondsLeft;
    /// <summary>Localized status/error line, empty if none.</summary>
    public readonly string StatusMessage;

    public IS14AtmUiState(
        IS14AtmScreen screen,
        string cardOwner,
        NetEntity? idCard,
        int accountNumber,
        int balance,
        int attemptsLeft,
        int lockSecondsLeft,
        string statusMessage)
    {
        Screen = screen;
        CardOwner = cardOwner;
        IdCard = idCard;
        AccountNumber = accountNumber;
        Balance = balance;
        AttemptsLeft = attemptsLeft;
        LockSecondsLeft = lockSecondsLeft;
        StatusMessage = statusMessage;
    }
}

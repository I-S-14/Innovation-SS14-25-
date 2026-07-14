namespace Content.Shared._IS14.Economy;

public sealed class BankAccount
{
    public int AccountNumber { get; }
    public int Pin { get; set; }
    public int Balance { get; set; }

    /// <summary>
    /// Whether the owner has set their own PIN. Until then the ATM offers
    /// to pick one on first login instead of asking for the generated PIN.
    /// </summary>
    public bool PinSet { get; set; }

    /// <summary>Consecutive failed PIN entries; reset on successful login.</summary>
    public int FailedPinAttempts { get; set; }

    /// <summary>Game time until which ATM login is blocked after too many failed PIN entries.</summary>
    public TimeSpan? LockedUntil { get; set; }

    public BankAccount(int accountNumber, int pin, int balance)
    {
        AccountNumber = accountNumber;
        Pin = pin;
        Balance = balance;
    }
}

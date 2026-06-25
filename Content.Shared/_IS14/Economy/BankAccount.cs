namespace Content.Shared._IS14.Economy;

public sealed class BankAccount
{
    public int AccountNumber { get; }
    public int Pin { get; set; }
    public int Balance { get; set; }

    public BankAccount(int accountNumber, int pin, int balance)
    {
        AccountNumber = accountNumber;
        Pin = pin;
        Balance = balance;
    }
}

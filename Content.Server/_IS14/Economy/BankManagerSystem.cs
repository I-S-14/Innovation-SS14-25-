using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Robust.Shared.Random;

namespace Content.Server._IS14.Economy;

public sealed class BankManagerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<int, BankAccount> _accounts = new();
    private int _nextId = 1;

    public BankAccount? GetAccount(int accountNumber)
        => _accounts.TryGetValue(accountNumber, out var account) ? account : null;

    /// <summary>
    /// Changes the balance of an account by delta and raises <see cref="EconomyTransactionEvent"/> on success.
    /// Returns false if the account doesn't exist or the delta would push the balance below zero.
    /// </summary>
    public bool TryChangeBalance(int accountNumber, int delta, out int newBalance, string description = "", EntityUid? sourceEntity = null, Guid? groupId = null)
    {
        newBalance = 0;

        var account = GetAccount(accountNumber);
        if (account == null || account.Balance + delta < 0)
            return false;

        account.Balance += delta;
        newBalance = account.Balance;
        RaiseLocalEvent(new EconomyTransactionEvent(accountNumber, delta, newBalance, description, sourceEntity, groupId));
        return true;
    }

    public BankAccount CreateAccount(int initialBalance = 0)
    {
        var pin = _random.Next(1000, 10000);
        var account = new BankAccount(_nextId++, pin, initialBalance);
        _accounts[account.AccountNumber] = account;
        return account;
    }

    public bool RemoveAccount(int accountNumber)
        => _accounts.Remove(accountNumber);

    public IReadOnlyCollection<BankAccount> GetAllAccounts()
        => _accounts.Values;
}

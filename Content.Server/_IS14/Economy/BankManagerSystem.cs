using Content.Shared._IS14.Economy;
using Robust.Shared.Random;

namespace Content.Server._IS14.Economy;

public sealed class BankManagerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<int, BankAccount> _accounts = new();
    private int _nextId = 1;

    public BankAccount? GetAccount(int accountNumber)
        => _accounts.TryGetValue(accountNumber, out var account) ? account : null;

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

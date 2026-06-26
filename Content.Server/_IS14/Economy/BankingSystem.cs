using Content.Server.Access.Systems;
using Content.Shared._IS14.Economy;

namespace Content.Server._IS14.Economy;

/// <summary>
/// Utility system for reading and modifying bank balances via entity ID cards.
/// </summary>
public sealed class BankingSystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;

    /// <summary>
    /// Finds the first ID card with a bank account on the entity and returns its balance.
    /// </summary>
    public bool TryGetEntityBalance(EntityUid entity, out int balance)
    {
        balance = 0;

        if (!_idCard.TryFindIdCard(entity, out var idCard))
            return false;

        if (!TryComp<BankAccountHolderComponent>(idCard, out var holder))
            return false;

        var account = _bankManager.GetAccount(holder.AccountNumber);
        if (account == null)
            return false;

        balance = account.Balance;
        return true;
    }

    /// <summary>
    /// Changes the balance of the entity's bank account by delta.
    /// Returns false if no account found or balance would go negative.
    /// </summary>
    public bool TryChangeBalance(EntityUid entity, int delta, out int newBalance)
    {
        newBalance = 0;

        if (!_idCard.TryFindIdCard(entity, out var idCard))
            return false;

        if (!TryComp<BankAccountHolderComponent>(idCard, out var holder))
            return false;

        var account = _bankManager.GetAccount(holder.AccountNumber);
        if (account == null)
            return false;

        if (account.Balance + delta < 0)
            return false;

        account.Balance += delta;
        newBalance = account.Balance;
        return true;
    }
}

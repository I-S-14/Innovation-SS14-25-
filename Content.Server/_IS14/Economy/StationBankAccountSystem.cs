using Content.Server._IS14.Economy.Components;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy;

/// <summary>
/// Creates station bank accounts at round start based on <see cref="StationEconomyComponent"/>
/// declared in map prototypes.
/// </summary>
public sealed class StationBankAccountSystem : EntitySystem
{
    /// <summary>Well-known account prototype ID of the station treasury.</summary>
    public const string Treasury = "StationTreasury";

    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized);
    }

    private void OnStationInitialized(StationInitializedEvent ev)
    {
        if (!TryComp<StationEconomyComponent>(ev.Station, out var economy))
            return;

        var accounts = EnsureComp<StationBankAccountsComponent>(ev.Station);

        foreach (var protoId in economy.Accounts)
        {
            if (!_prototypes.TryIndex(protoId, out StationAccountPrototype? proto))
            {
                Log.Warning($"StationBankAccountSystem: unknown stationBankAccount prototype '{protoId}', skipping.");
                continue;
            }

            var account = _bankManager.CreateAccount(proto.InitialBalance);
            accounts.AccountNumbers[(string)protoId] = account.AccountNumber;
            Log.Info($"Created station account '{protoId}' ({proto.DisplayName}) with balance {proto.InitialBalance}, account #{account.AccountNumber}.");
        }
    }

    /// <summary>
    /// Returns the live bank account for a given prototype ID on a station entity,
    /// or null if the station has no such account.
    /// </summary>
    public BankAccount? GetStationAccount(EntityUid station, string protoId)
    {
        if (!TryComp<StationBankAccountsComponent>(station, out var comp))
            return null;

        if (!comp.AccountNumbers.TryGetValue(protoId, out var accountNumber))
            return null;

        return _bankManager.GetAccount(accountNumber);
    }

    /// <summary>
    /// Tries to change the balance of a named station account.
    /// Returns false if the account doesn't exist or the delta would push balance below zero.
    /// </summary>
    public bool TryChangeStationBalance(EntityUid station, string protoId, int delta, out int newBalance)
    {
        newBalance = 0;
        var account = GetStationAccount(station, protoId);
        if (account == null)
            return false;

        var result = account.Balance + delta;
        if (result < 0)
            return false;

        account.Balance = result;
        newBalance = result;
        return true;
    }
}

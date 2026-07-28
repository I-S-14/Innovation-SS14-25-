using Content.Server._IS14.Economy.Components;
using Content.Server.Station.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Economy;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy;

/// <summary>
/// Creates station bank accounts at round start based on <see cref="StationEconomyComponent"/>
/// declared in map prototypes, and tracks each account's bonus pool.
/// </summary>
public sealed class StationBankAccountSystem : EntitySystem
{
    /// <summary>Well-known account prototype ID of the station treasury.</summary>
    public const string Treasury = "StationTreasury";

    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

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
        var fraction = _cfg.GetCVar(IS14CVars.PayrollBonusIncomeFraction);

        foreach (var protoId in economy.Accounts)
        {
            if (!_prototypes.TryIndex(protoId, out StationAccountPrototype? proto))
            {
                Log.Warning($"StationBankAccountSystem: unknown stationBankAccount prototype '{protoId}', skipping.");
                continue;
            }

            var account = _bankManager.CreateAccount(proto.InitialBalance);
            accounts.AccountNumbers[(string)protoId] = account.AccountNumber;

            // The starting budget counts as the account's first income.
            accounts.BonusPools[(string)protoId] = Math.Max(0, (int)(proto.InitialBalance * fraction));

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
    /// <param name="accrueBonusPool">
    /// Whether income should grow the account's bonus pool. Pass false when refunding a failed
    /// operation, so undoing a debit doesn't count as fresh income.
    /// </param>
    public bool TryChangeStationBalance(EntityUid station, string protoId, int delta, out int newBalance, bool accrueBonusPool = true)
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

        if (delta > 0 && accrueBonusPool)
            AccrueBonusPool(station, protoId, delta);

        return true;
    }

    // ── Bonus pool ────────────────────────────────────────────────────────────

    /// <summary>Credits currently set aside for bonuses on this account.</summary>
    public int GetBonusPool(EntityUid station, string protoId)
    {
        if (!TryComp<StationBankAccountsComponent>(station, out var comp))
            return 0;

        return comp.BonusPools.GetValueOrDefault(protoId, 0);
    }

    /// <summary>
    /// Takes credits out of the bonus pool. Returns false when the pool is too small,
    /// leaving it untouched.
    /// </summary>
    public bool TryConsumeBonusPool(EntityUid station, string protoId, int amount)
    {
        if (amount <= 0 || !TryComp<StationBankAccountsComponent>(station, out var comp))
            return false;

        var pool = comp.BonusPools.GetValueOrDefault(protoId, 0);
        if (pool < amount)
            return false;

        comp.BonusPools[protoId] = pool - amount;
        return true;
    }

    /// <summary>
    /// Puts credits straight into the bonus pool without touching the account balance.
    /// Used for awards that are meant to be handed out personally rather than spent
    /// on department supplies.
    /// </summary>
    public void AddBonusPool(EntityUid station, string protoId, int amount)
    {
        if (amount <= 0 || !TryComp<StationBankAccountsComponent>(station, out var comp))
            return;

        comp.BonusPools[protoId] = comp.BonusPools.GetValueOrDefault(protoId, 0) + amount;
    }

    /// <summary>Puts a share of incoming money aside for bonuses.</summary>
    private void AccrueBonusPool(EntityUid station, string protoId, int income)
    {
        if (!TryComp<StationBankAccountsComponent>(station, out var comp))
            return;

        var share = (int)(income * _cfg.GetCVar(IS14CVars.PayrollBonusIncomeFraction));
        if (share <= 0)
            return;

        comp.BonusPools[protoId] = comp.BonusPools.GetValueOrDefault(protoId, 0) + share;
    }
}

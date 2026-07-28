using Content.Server._IS14.Economy.Components;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.Treasury;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.Treasury;

public sealed class TreasuryConsoleSystem : EntitySystem
{
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);

        Subs.BuiEvents<TreasuryConsoleComponent>(TreasuryConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<TreasuryTransferMessage>(OnTransfer);
        });
    }

    private void OnOpened(Entity<TreasuryConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        SendState(ent.Owner);
    }

    /// <summary>Keeps open consoles live: any transaction may change a station balance.</summary>
    private void OnTransaction(EconomyTransactionEvent ev)
    {
        var query = EntityQueryEnumerator<TreasuryConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, TreasuryConsoleUiKey.Key))
                SendState(uid);
        }
    }

    private void OnTransfer(Entity<TreasuryConsoleComponent> ent, ref TreasuryTransferMessage args)
    {
        if (_station.GetOwningStation(ent.Owner) is not { } station)
            return;

        if (args.Amount <= 0)
        {
            SendState(ent.Owner, Loc.GetString("is14-treasury-status-bad-amount"));
            return;
        }

        if (args.TargetAccount == StationBankAccountSystem.Treasury
            || _stationBank.GetStationAccount(station, args.TargetAccount) is not { } target)
        {
            SendState(ent.Owner, Loc.GetString("is14-treasury-status-bad-target"));
            return;
        }

        if (!_stationBank.TryChangeStationBalance(station, StationBankAccountSystem.Treasury, -args.Amount, out var treasuryBalance))
        {
            SendState(ent.Owner, Loc.GetString("is14-treasury-status-insufficient"));
            return;
        }

        _stationBank.TryChangeStationBalance(station, args.TargetAccount, args.Amount, out var targetBalance);

        var targetName = args.TargetAccount;
        if (_prototypes.TryIndex(args.TargetAccount, out StationAccountPrototype? proto))
            targetName = proto.DisplayName;

        var treasury = _stationBank.GetStationAccount(station, StationBankAccountSystem.Treasury)!;
        RaiseLocalEvent(new EconomyTransactionEvent(treasury.AccountNumber, -args.Amount, treasuryBalance,
            Loc.GetString("economy-transaction-allocation-out", ("target", targetName)), ent.Owner));
        RaiseLocalEvent(new EconomyTransactionEvent(target.AccountNumber, args.Amount, targetBalance,
            Loc.GetString("economy-transaction-allocation-in"), ent.Owner));

        SendState(ent.Owner, Loc.GetString("is14-treasury-status-done", ("amount", args.Amount), ("target", targetName)));
    }

    private void SendState(EntityUid console, string status = "")
    {
        var accounts = new List<TreasuryAccountEntry>();

        if (_station.GetOwningStation(console) is { } station
            && TryComp<StationEconomyComponent>(station, out var economy)
            && HasComp<StationBankAccountsComponent>(station))
        {
            foreach (var protoId in economy.Accounts)
            {
                if (!_prototypes.TryIndex(protoId, out var proto))
                    continue;

                if (_stationBank.GetStationAccount(station, protoId) is not { } account)
                    continue;

                accounts.Add(new TreasuryAccountEntry(
                    protoId,
                    proto.DisplayName,
                    account.Balance,
                    protoId.Id == StationBankAccountSystem.Treasury));
            }
        }

        _ui.SetUiState(console, TreasuryConsoleUiKey.Key, new TreasuryConsoleUiState(accounts, status));
    }
}

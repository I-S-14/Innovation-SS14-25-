using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.CriminalRecords.Systems;
using Content.Server.GameTicking;
using Content.Server.StationRecords.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.Fines;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.CriminalRecords;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Server._IS14.Economy.Fines;

/// <summary>
/// Fines written by Security. A fine is a debt, not a deduction: nobody's account is
/// touched without their say-so. Refusing to pay leaves you on the wanted list, which
/// turns a fine into a conversation instead of a cell timer.
/// </summary>
public sealed class FineSystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly SoundPathSpecifier FineSound = new("/Audio/Effects/Cargo/buzz_sigh.ogg");

    // ── Writing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a fine against a crew member. Returns null when the amount is out of
    /// bounds or the record doesn't exist.
    /// </summary>
    public FineRecord? TryIssueFine(EntityUid station, uint recordId, string article, int amount, string officerName)
    {
        if (amount <= 0 || amount > _cfg.GetCVar(IS14CVars.FineMaxAmount))
            return null;

        var key = new StationRecordKey(recordId, station);
        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var record))
            return null;

        var fines = EnsureComp<StationFinesComponent>(station);
        var accountNumber = FindAccountByName(record.Name);

        var fine = new FineRecord(
            fines.NextFineId++,
            record.Name,
            accountNumber,
            recordId,
            officerName,
            article,
            amount,
            _ticker.RoundDuration());

        fines.Fines.Add(fine);

        _criminalRecords.TryAddHistory(key,
            Loc.GetString("is14-fine-history-issued", ("article", article), ("amount", amount)),
            officerName);

        MarkWanted(station, fines, key, fine);
        NotifyOffender(record.Name, Loc.GetString("is14-fine-notify-issued", ("article", article), ("amount", amount)));

        return fine;
    }

    /// <summary>Cancels a fine. The record stays in the list so a bad call remains visible.</summary>
    public bool TryVoidFine(EntityUid station, uint fineId, string officerName)
    {
        if (!TryComp<StationFinesComponent>(station, out var fines))
            return false;

        var fine = fines.Fines.FirstOrDefault(f => f.Id == fineId);
        if (fine == null || !fine.Outstanding)
            return false;

        fine.Voided = true;

        if (fine.RecordId is { } recordId)
        {
            var key = new StationRecordKey(recordId, station);
            _criminalRecords.TryAddHistory(key,
                Loc.GetString("is14-fine-history-voided", ("article", fine.Article)),
                officerName);

            ClearWantedIfSettled(station, fines, key, recordId);
        }

        NotifyOffender(fine.TargetName, Loc.GetString("is14-fine-notify-voided", ("article", fine.Article)));
        return true;
    }

    // ── Paying ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pays a fine from a bank account. The credits go to the station treasury —
    /// fines are revenue for the station, never for the officer who wrote them.
    /// </summary>
    public bool TryPayFine(EntityUid station, uint fineId, int payerAccount, out string status)
    {
        status = string.Empty;

        if (!TryComp<StationFinesComponent>(station, out var fines))
            return false;

        var fine = fines.Fines.FirstOrDefault(f => f.Id == fineId);
        if (fine == null || !fine.Outstanding)
        {
            status = Loc.GetString("is14-fine-status-not-found");
            return false;
        }

        var groupId = Guid.NewGuid();

        if (!_bankManager.TryChangeBalance(payerAccount, -fine.Amount, out _,
                Loc.GetString("economy-transaction-fine-paid", ("article", fine.Article)), null, groupId))
        {
            status = Loc.GetString("is14-fine-status-insufficient");
            return false;
        }

        fine.Paid = true;

        // Treasury credit is best-effort: an off-station terminal still clears the debt.
        if (_stationBank.GetStationAccount(station, StationBankAccountSystem.Treasury) is { } treasury
            && _stationBank.TryChangeStationBalance(station, StationBankAccountSystem.Treasury, fine.Amount, out var newBalance))
        {
            RaiseLocalEvent(new EconomyTransactionEvent(treasury.AccountNumber, fine.Amount, newBalance,
                Loc.GetString("economy-transaction-fine-collected", ("name", fine.TargetName)), null, groupId));
        }

        if (fine.RecordId is { } recordId)
        {
            var key = new StationRecordKey(recordId, station);
            _criminalRecords.TryAddHistory(key,
                Loc.GetString("is14-fine-history-paid", ("article", fine.Article), ("amount", fine.Amount)));

            ClearWantedIfSettled(station, fines, key, recordId);
        }

        status = Loc.GetString("is14-fine-status-paid", ("amount", fine.Amount));
        return true;
    }

    /// <summary>
    /// Fines that can be paid from this account: ones matched to the account itself,
    /// plus any written in the cardholder's name before their card was found.
    /// </summary>
    public List<FineRecord> GetPayableFines(EntityUid station, int accountNumber, string holderName)
    {
        if (!TryComp<StationFinesComponent>(station, out var fines))
            return new List<FineRecord>();

        return fines.Fines
            .Where(f => f.Outstanding
                        && (f.AccountNumber == accountNumber
                            || (f.AccountNumber == null && f.TargetName == holderName)))
            .ToList();
    }

    public List<FineRecord> GetFines(EntityUid station)
    {
        return TryComp<StationFinesComponent>(station, out var fines)
            ? fines.Fines
            : new List<FineRecord>();
    }

    /// <summary>Total a crew member still owes.</summary>
    public int GetOutstandingAmount(EntityUid station, uint recordId)
    {
        if (!TryComp<StationFinesComponent>(station, out var fines))
            return 0;

        return fines.Fines
            .Where(f => f.RecordId == recordId && f.Outstanding)
            .Sum(f => f.Amount);
    }

    // ── Wanted status ─────────────────────────────────────────────────────────

    private void MarkWanted(EntityUid station, StationFinesComponent fines, StationRecordKey key, FineRecord fine)
    {
        if (!_cfg.GetCVar(IS14CVars.FineSetsWantedStatus))
            return;

        if (!_records.TryGetRecord<CriminalRecord>(key, out var record) || record.Status != SecurityStatus.None)
            return;

        var reason = Loc.GetString("is14-fine-wanted-reason", ("article", fine.Article), ("amount", fine.Amount));
        if (_criminalRecords.TryChangeStatus(key, SecurityStatus.Wanted, reason, fine.OfficerName))
            fines.WantedByFine.Add(key.Id);
    }

    /// <summary>
    /// Drops the wanted status once nothing is owed — but only if the fine system is
    /// what put it there in the first place.
    /// </summary>
    private void ClearWantedIfSettled(EntityUid station, StationFinesComponent fines, StationRecordKey key, uint recordId)
    {
        if (!fines.WantedByFine.Contains(recordId))
            return;

        if (GetOutstandingAmount(station, recordId) > 0)
            return;

        fines.WantedByFine.Remove(recordId);

        if (_records.TryGetRecord<CriminalRecord>(key, out var record) && record.Status == SecurityStatus.Wanted)
            _criminalRecords.TryChangeStatus(key, SecurityStatus.None, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the bank account of the ID card carrying this name. Fines are written
    /// against a person, and the card is how a person maps to money.
    /// </summary>
    private int? FindAccountByName(string name)
    {
        var query = EntityQueryEnumerator<BankAccountHolderComponent, IdCardComponent>();
        while (query.MoveNext(out _, out var holder, out var idCard))
        {
            if (idCard.FullName == name)
                return holder.AccountNumber;
        }

        return null;
    }

    /// <summary>
    /// Speaks the notification through the offender's own ID card, the same way salary
    /// payments announce themselves. Being fined in public is part of the punishment.
    /// </summary>
    private void NotifyOffender(string name, string message)
    {
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var idCard))
        {
            if (idCard.FullName != name)
                continue;

            _audio.PlayPvs(FineSound, uid, AudioParams.Default.WithVolume(-2f));
            _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak,
                hideChat: false, hideLog: true, ignoreActionBlocker: true);
            return;
        }
    }
}

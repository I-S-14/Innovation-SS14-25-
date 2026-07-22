using Content.Server.Chat.Systems;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared.Chat;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy;

public sealed class SalarySystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundPathSpecifier SalarySound = new("/Audio/Items/appraiser.ogg");
    private static readonly SoundPathSpecifier DelaySound = new("/Audio/Effects/Cargo/buzz_sigh.ogg");

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<JobSalaryComponent>();

        while (query.MoveNext(out var uid, out var salary))
        {
            if (now < salary.NextPaymentTime)
                continue;

            salary.NextPaymentTime += TimeSpan.FromSeconds(salary.SalaryIntervalSeconds);
            Pay(salary);
        }
    }

    public void PaySalaryNow(EntityUid uid, JobSalaryComponent salary)
    {
        salary.NextPaymentTime = _timing.CurTime + TimeSpan.FromSeconds(salary.SalaryIntervalSeconds);
        Pay(salary);
    }

    public void PayAllSalariesNow()
    {
        var query = EntityQueryEnumerator<JobSalaryComponent>();
        while (query.MoveNext(out var uid, out var salary))
            PaySalaryNow(uid, salary);
    }

    /// <summary>
    /// Pays the salary plus any owed backlog from the department fund.
    /// If the fund can't cover it, the payment is skipped and the debt grows.
    /// </summary>
    private void Pay(JobSalaryComponent salary)
    {
        var account = _bankManager.GetAccount(salary.AccountNumber);
        if (account == null)
            return;

        var due = salary.Salary + salary.OwedSalary;
        if (due <= 0)
            return;

        if (!TryDebitFund(salary, due))
        {
            salary.OwedSalary += salary.Salary;
            AnnounceDelay(salary.IdCardEntity, salary.OwedSalary);
            return;
        }

        salary.OwedSalary = 0;
        account.Balance += due;
        RaiseLocalEvent(new EconomyTransactionEvent(salary.AccountNumber, due, account.Balance, Loc.GetString("economy-transaction-salary")));
        AnnouncePayment(salary.IdCardEntity, due, account.Balance);
    }

    /// <summary>
    /// Debits the salary from the station fund configured for this job.
    /// Returns true without a debit when the station or fund doesn't exist,
    /// so maps without an economy still pay salaries.
    /// </summary>
    private bool TryDebitFund(JobSalaryComponent salary, int amount)
    {
        if (salary.PayerAccount is not { } payer)
            return true;

        if (salary.Station is not { } station || !Exists(station))
            return true;

        var fund = _stationBank.GetStationAccount(station, payer);
        if (fund == null)
            return true;

        if (!_stationBank.TryChangeStationBalance(station, payer, -amount, out var newBalance))
            return false;

        RaiseLocalEvent(new EconomyTransactionEvent(fund.AccountNumber, -amount, newBalance, Loc.GetString("economy-transaction-salary-fund")));
        return true;
    }

    private void AnnouncePayment(EntityUid? idCard, int salary, int newBalance)
    {
        if (!TryGetCard(idCard, out var card))
            return;

        _audio.PlayPvs(SalarySound, card, AudioParams.Default.WithVolume(-2f));

        var message = Loc.GetString("bank-salary-notification",
            ("salary", salary),
            ("balance", newBalance));

        _chat.TrySendInGameICMessage(card, message, InGameICChatType.Speak, hideChat: false, hideLog: true, ignoreActionBlocker: true);
    }

    private void AnnounceDelay(EntityUid? idCard, int owed)
    {
        if (!TryGetCard(idCard, out var card))
            return;

        _audio.PlayPvs(DelaySound, card, AudioParams.Default.WithVolume(-2f));

        var message = Loc.GetString("bank-salary-delayed", ("owed", owed));

        _chat.TrySendInGameICMessage(card, message, InGameICChatType.Speak, hideChat: false, hideLog: true, ignoreActionBlocker: true);
    }

    private bool TryGetCard(EntityUid? idCard, out EntityUid card)
    {
        card = default;
        if (idCard == null || !Exists(idCard.Value))
            return false;

        card = idCard.Value;
        return true;
    }
}

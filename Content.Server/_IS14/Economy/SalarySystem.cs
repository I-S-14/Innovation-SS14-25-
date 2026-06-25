using Content.Server.Chat.Systems;
using Content.Shared._IS14.Economy;
using Content.Shared.Chat;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy;

public sealed class SalarySystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundPathSpecifier SalarySound = new("/Audio/Items/appraiser.ogg");

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

            var account = _bankManager.GetAccount(salary.AccountNumber);
            if (account == null)
                continue;

            account.Balance += salary.Salary;
            AnnounceFromCard(salary.IdCardEntity, salary.Salary, account.Balance);
        }
    }

    public void PaySalaryNow(EntityUid uid, JobSalaryComponent salary)
    {
        var account = _bankManager.GetAccount(salary.AccountNumber);
        if (account == null)
            return;

        account.Balance += salary.Salary;
        salary.NextPaymentTime = _timing.CurTime + TimeSpan.FromSeconds(salary.SalaryIntervalSeconds);
        AnnounceFromCard(salary.IdCardEntity, salary.Salary, account.Balance);
    }

    public void PayAllSalariesNow()
    {
        var query = EntityQueryEnumerator<JobSalaryComponent>();
        while (query.MoveNext(out var uid, out var salary))
            PaySalaryNow(uid, salary);
    }

    private void AnnounceFromCard(EntityUid? idCard, int salary, int newBalance)
    {
        if (idCard == null || !Exists(idCard.Value))
            return;

        var card = idCard.Value;

        _audio.PlayPvs(SalarySound, card, AudioParams.Default.WithVolume(-2f));

        var message = Loc.GetString("bank-salary-notification",
            ("salary", salary),
            ("balance", newBalance));

        _chat.TrySendInGameICMessage(card, message, InGameICChatType.Speak, hideChat: false, hideLog: true, ignoreActionBlocker: true);
    }
}

using Content.Server.Chat.Managers;
using Content.Shared._IS14.Economy;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy;

public sealed class SalarySystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundPathSpecifier SalarySound = new("/Audio/Item/appriser.ogg");
    private static readonly Color SalaryColor = Color.FromSrgb(new Color(0.4f, 0.8f, 0.4f));

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
            NotifyPlayer(uid, salary.Salary, account.Balance);

        }
    }

    private void NotifyPlayer(EntityUid uid, int salary, int newBalance)
    {
        if (!_mind.TryGetMind(uid, out _, out var mind) || mind.UserId == null)
            return;

        if (!_player.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        _audio.PlayEntity(SalarySound, session, uid, AudioParams.Default.WithVolume(-4f));

        var message = Loc.GetString("bank-salary-notification",
            ("salary", salary),
            ("balance", newBalance));
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));

        _chatManager.ChatMessageToOne(
            ChatChannel.Server,
            message,
            wrappedMessage,
            default,
            false,
            session.Channel,
            SalaryColor);
    }
}

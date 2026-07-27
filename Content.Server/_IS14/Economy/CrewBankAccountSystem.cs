using Content.Server.Access.Systems;
using Content.Shared._IS14.Economy;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy;

public sealed class CrewBankAccountSystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        if (!_prototypes.TryIndex<JobEconomyPrototype>(ev.JobId, out var economy))
            return;

        var balance = _random.Next(economy.MinBalance, economy.MaxBalance + 1);
        var account = _bankManager.CreateAccount(balance);

        // Bind account to the crew member's ID card
        EntityUid? idCardUid = null;
        if (_idCard.TryFindIdCard(ev.Mob, out var idCard))
        {
            var holder = EnsureComp<BankAccountHolderComponent>(idCard);
            holder.AccountNumber = account.AccountNumber;
            idCardUid = idCard.Owner;
        }

        // Add salary tracking to the mob entity
        var salary = EnsureComp<JobSalaryComponent>(ev.Mob);
        salary.AccountNumber = account.AccountNumber;
        salary.Salary = economy.Salary;
        salary.BaseSalary = economy.Salary;
        salary.JobProtoId = ev.JobId;
        salary.SalaryIntervalSeconds = economy.SalaryIntervalSeconds;
        salary.NextPaymentTime = _timing.CurTime + TimeSpan.FromSeconds(economy.SalaryIntervalSeconds);
        salary.IdCardEntity = idCardUid;
        salary.Station = ev.Station;
        salary.PayerAccount = economy.PayerAccount;
    }
}

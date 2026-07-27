using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.Payroll;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.Payroll;

/// <summary>
/// Backs the department payroll console: a head adjusts the salaries of everyone paid
/// by their fund and hands out bonuses or fines, all settled against that same fund.
/// </summary>
public sealed class PayrollConsoleSystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly StationBankAccountSystem _stationBank = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundPathSpecifier GoodNewsSound = new("/Audio/Items/appraiser.ogg");
    private static readonly SoundPathSpecifier BadNewsSound = new("/Audio/Effects/Cargo/buzz_sigh.ogg");

    /// <summary>
    /// How often a console open in the background is refreshed. Rebuilding the state walks
    /// every crew member, so a salary tick paying a hundred people must not do it a hundred times.
    /// The head's own actions still update instantly.
    /// </summary>
    private static readonly TimeSpan PassiveRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<EntityUid> _pendingRefresh = new();
    private readonly List<EntityUid> _refreshBuffer = new();
    private TimeSpan _nextPassiveRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);

        Subs.BuiEvents<PayrollConsoleComponent>(PayrollConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
            subs.Event<PayrollSetSalaryMessage>(OnSetSalary);
            subs.Event<PayrollBonusMessage>(OnBonus);
            subs.Event<PayrollFineMessage>(OnFine);
        });
    }

    private void OnOpened(Entity<PayrollConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        SendState(ent);
    }

    /// <summary>
    /// Drops the stored state once the last viewer leaves. A BUI state lives on in the
    /// component and is replicated to everyone who can see the console, so leaving a
    /// stale payroll list there taxes every passer-by for the rest of the round.
    /// </summary>
    private void OnClosed(Entity<PayrollConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_ui.IsUiOpen(ent.Owner, PayrollConsoleUiKey.Key))
            return;

        _pendingRefresh.Remove(ent.Owner);
        _ui.SetUiState(ent.Owner, PayrollConsoleUiKey.Key, null);
    }

    /// <summary>Queues open consoles for a refresh: any transaction may change the fund balance.</summary>
    private void OnTransaction(EconomyTransactionEvent ev)
    {
        var query = EntityQueryEnumerator<PayrollConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, PayrollConsoleUiKey.Key))
                _pendingRefresh.Add(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingRefresh.Count == 0 || _timing.CurTime < _nextPassiveRefresh)
            return;

        _nextPassiveRefresh = _timing.CurTime + PassiveRefreshInterval;

        // Drain into a buffer first: SendState clears the console's pending flag,
        // which would otherwise mutate the set we're iterating.
        _refreshBuffer.Clear();
        _refreshBuffer.AddRange(_pendingRefresh);
        _pendingRefresh.Clear();

        foreach (var uid in _refreshBuffer)
        {
            if (TryComp<PayrollConsoleComponent>(uid, out var console) && _ui.IsUiOpen(uid, PayrollConsoleUiKey.Key))
                SendState((uid, console));
        }
    }

    // ── Operations ────────────────────────────────────────────────────────────

    private void OnSetSalary(Entity<PayrollConsoleComponent> ent, ref PayrollSetSalaryMessage args)
    {
        if (!TryResolveEmployee(ent, args.Employee, args.Actor, out var employee, out var salary, out var denial))
        {
            Deny(ent, denial);
            return;
        }

        var (min, max) = GetSalaryRange(salary);
        if (args.Salary < min || args.Salary > max)
        {
            Deny(ent, Loc.GetString("is14-payroll-status-bad-salary", ("min", min), ("max", max)));
            return;
        }

        var previous = salary.Salary;
        if (args.Salary == previous)
            return;

        salary.Salary = args.Salary;

        var raised = args.Salary > previous;
        var name = GetEmployeeName(employee, salary);

        Notify(salary,
            Loc.GetString(raised ? "is14-payroll-notify-raise" : "is14-payroll-notify-cut", ("salary", args.Salary)),
            raised);

        // No money moves yet, but the fund's future outflow just changed — the monitor
        // should show that alongside the payments it causes.
        ReportToMonitor(salary.AccountNumber,
            Loc.GetString("economy-transaction-salary-changed",
                ("name", name), ("old", previous), ("new", args.Salary)),
            ent.Owner);

        LogAction(ent,
            Loc.GetString(raised ? "is14-payroll-log-raise" : "is14-payroll-log-cut",
                ("name", name), ("old", previous), ("new", args.Salary)),
            raised ? PayrollLogKind.Positive : PayrollLogKind.Negative);

        SendState(ent, Loc.GetString("is14-payroll-status-salary-set", ("name", name), ("salary", args.Salary)));
    }

    private void OnBonus(Entity<PayrollConsoleComponent> ent, ref PayrollBonusMessage args)
    {
        if (!TryResolveEmployee(ent, args.Employee, args.Actor, out var employee, out var salary, out var denial))
        {
            Deny(ent, denial);
            return;
        }

        if (!TryGetFund(ent, out var station, out var fund))
        {
            Deny(ent, Loc.GetString("is14-payroll-status-no-fund"));
            return;
        }

        // Bonuses come out of the pool the department accrued from its income, not the raw budget.
        var pool = _stationBank.GetBonusPool(station, ent.Comp.ManagedAccount);
        if (args.Amount <= 0 || args.Amount > pool)
        {
            Deny(ent, Loc.GetString("is14-payroll-status-bad-bonus", ("max", pool)));
            return;
        }

        if (!_stationBank.TryChangeStationBalance(station, ent.Comp.ManagedAccount, -args.Amount, out var fundBalance))
        {
            Deny(ent, Loc.GetString("is14-payroll-status-fund-insufficient"));
            return;
        }

        var name = GetEmployeeName(employee, salary);
        var group = Guid.NewGuid();

        if (!_bankManager.TryChangeBalance(salary.AccountNumber, args.Amount, out _,
                Loc.GetString("economy-transaction-bonus"), ent.Owner, group))
        {
            // Employee account vanished — put the money back rather than burning it.
            // Undoing our own debit isn't income, so it must not grow the pool.
            _stationBank.TryChangeStationBalance(station, ent.Comp.ManagedAccount, args.Amount, out _, accrueBonusPool: false);
            Deny(ent, Loc.GetString("is14-payroll-status-no-account"));
            return;
        }

        _stationBank.TryConsumeBonusPool(station, ent.Comp.ManagedAccount, args.Amount);

        RaiseLocalEvent(new EconomyTransactionEvent(fund.AccountNumber, -args.Amount, fundBalance,
            Loc.GetString("economy-transaction-bonus-fund", ("name", name)), ent.Owner, group));

        Notify(salary, Loc.GetString("is14-payroll-notify-bonus", ("amount", args.Amount)), true);

        LogAction(ent, Loc.GetString("is14-payroll-log-bonus", ("name", name), ("amount", args.Amount)),
            PayrollLogKind.Positive);

        SendState(ent, Loc.GetString("is14-payroll-status-bonus-paid", ("name", name), ("amount", args.Amount)));
    }

    private void OnFine(Entity<PayrollConsoleComponent> ent, ref PayrollFineMessage args)
    {
        if (!TryResolveEmployee(ent, args.Employee, args.Actor, out var employee, out var salary, out var denial))
        {
            Deny(ent, denial);
            return;
        }

        var maxFine = _cfg.GetCVar(IS14CVars.PayrollMaxFine);
        if (args.Amount <= 0 || args.Amount > maxFine)
        {
            Deny(ent, Loc.GetString("is14-payroll-status-bad-fine", ("max", maxFine)));
            return;
        }

        if (!TryGetFund(ent, out var station, out var fund))
        {
            Deny(ent, Loc.GetString("is14-payroll-status-no-fund"));
            return;
        }

        var account = _bankManager.GetAccount(salary.AccountNumber);
        if (account == null)
        {
            Deny(ent, Loc.GetString("is14-payroll-status-no-account"));
            return;
        }

        // An employee sitting at zero can't be squeezed dry — collect whatever is there.
        var collected = Math.Min(args.Amount, account.Balance);
        if (collected <= 0)
        {
            Deny(ent, Loc.GetString("is14-payroll-status-employee-broke"));
            return;
        }

        var name = GetEmployeeName(employee, salary);
        var group = Guid.NewGuid();

        if (!_bankManager.TryChangeBalance(salary.AccountNumber, -collected, out _,
                Loc.GetString("economy-transaction-fine"), ent.Owner, group))
        {
            Deny(ent, Loc.GetString("is14-payroll-status-no-account"));
            return;
        }

        _stationBank.TryChangeStationBalance(station, ent.Comp.ManagedAccount, collected, out var fundBalance);
        RaiseLocalEvent(new EconomyTransactionEvent(fund.AccountNumber, collected, fundBalance,
            Loc.GetString("economy-transaction-fine-fund", ("name", name)), ent.Owner, group));

        Notify(salary, Loc.GetString("is14-payroll-notify-fine", ("amount", collected)), false);

        LogAction(ent, Loc.GetString("is14-payroll-log-fine", ("name", name), ("amount", collected)),
            PayrollLogKind.Negative);

        SendState(ent, Loc.GetString("is14-payroll-status-fine-collected", ("name", name), ("amount", collected)));
    }

    // ── Limits ────────────────────────────────────────────────────────────────

    private (int Min, int Max) GetSalaryRange(JobSalaryComponent salary)
    {
        // Crew spawned before BaseSalary existed fall back to their current salary.
        var basis = salary.BaseSalary > 0 ? salary.BaseSalary : salary.Salary;
        var min = (int)(basis * _cfg.GetCVar(IS14CVars.PayrollSalaryMinMultiplier));
        var max = (int)(basis * _cfg.GetCVar(IS14CVars.PayrollSalaryMaxMultiplier));
        return (min, Math.Max(min, max));
    }

    /// <summary>
    /// What the head can actually pay out right now: the accrued pool, clipped by whatever
    /// is left in the fund — a pool bigger than the budget still can't be spent.
    /// </summary>
    private int GetPayableBonus(int bonusPool, int fundBalance)
        => Math.Max(0, Math.Min(bonusPool, fundBalance));

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a client-supplied employee and checks the console may act on them —
    /// never trust the entity in the message alone.
    /// </summary>
    private bool TryResolveEmployee(
        Entity<PayrollConsoleComponent> console,
        NetEntity netEmployee,
        EntityUid actor,
        out EntityUid employee,
        out JobSalaryComponent salary,
        out string denial)
    {
        employee = default;
        salary = default!;
        denial = string.Empty;

        if (!TryGetEntity(netEmployee, out var uid)
            || !TryComp<JobSalaryComponent>(uid, out var comp)
            || !IsManagedBy(console, uid.Value, comp))
        {
            denial = Loc.GetString("is14-payroll-status-not-subordinate");
            return false;
        }

        if (uid.Value == actor && !console.Comp.AllowSelfManagement)
        {
            denial = Loc.GetString("is14-payroll-status-no-self");
            return false;
        }

        employee = uid.Value;
        salary = comp;
        return true;
    }

    /// <summary>An employee belongs to a console when the console's fund is the one paying them.</summary>
    private bool IsManagedBy(Entity<PayrollConsoleComponent> console, EntityUid employee, JobSalaryComponent salary)
    {
        if (salary.PayerAccount != console.Comp.ManagedAccount.Id)
            return false;

        // Consoles on a station only reach that station's crew; off-station consoles reach everyone on their fund.
        if (_station.GetOwningStation(console.Owner) is { } station && salary.Station is { } payer && payer != station)
            return false;

        return true;
    }

    private bool TryGetFund(Entity<PayrollConsoleComponent> console, out EntityUid station, out BankAccount fund)
    {
        station = default;
        fund = default!;

        if (_station.GetOwningStation(console.Owner) is not { } owning)
            return false;

        if (_stationBank.GetStationAccount(owning, console.Comp.ManagedAccount) is not { } account)
            return false;

        station = owning;
        fund = account;
        return true;
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    /// <summary>Records an action in the console's own log tab.</summary>
    private void LogAction(Entity<PayrollConsoleComponent> console, string message, PayrollLogKind kind)
    {
        console.Comp.Log.Add(new PayrollLogEntry(_timing.CurTime, message, kind));

        var limit = Math.Max(1, _cfg.GetCVar(IS14CVars.PayrollLogLength));
        if (console.Comp.Log.Count > limit)
            console.Comp.Log.RemoveRange(0, console.Comp.Log.Count - limit);
    }

    /// <summary>
    /// Puts a moneyless payroll decision on the station-wide economy monitor.
    /// Delta is zero because nothing moved yet — only the future obligation changed.
    /// </summary>
    private void ReportToMonitor(int accountNumber, string description, EntityUid source)
    {
        var balance = _bankManager.GetAccount(accountNumber)?.Balance ?? 0;
        RaiseLocalEvent(new EconomyTransactionEvent(accountNumber, 0, balance, description, source));
    }

    /// <summary>Refuses an action: tells the head why and keeps the attempt in the console log.</summary>
    private void Deny(Entity<PayrollConsoleComponent> console, string reason)
    {
        LogAction(console, reason, PayrollLogKind.Denied);
        SendState(console, reason);
    }

    // ── Presentation ──────────────────────────────────────────────────────────

    private void SendState(Entity<PayrollConsoleComponent> console, string status = "")
    {
        var fundName = console.Comp.ManagedAccount.Id;
        if (_prototypes.TryIndex(console.Comp.ManagedAccount, out var proto))
            fundName = proto.DisplayName;

        var hasFund = TryGetFund(console, out var station, out var fund);
        var fundBalance = hasFund ? fund.Balance : 0;
        var bonusPool = hasFund ? _stationBank.GetBonusPool(station, console.Comp.ManagedAccount) : 0;

        var employees = new List<PayrollEmployeeEntry>();
        var query = EntityQueryEnumerator<JobSalaryComponent>();
        while (query.MoveNext(out var uid, out var salary))
        {
            if (!IsManagedBy(console, uid, salary))
                continue;

            var (min, max) = GetSalaryRange(salary);
            employees.Add(new PayrollEmployeeEntry(
                GetNetEntity(uid),
                GetEmployeeName(uid, salary),
                GetJobTitle(salary),
                salary.Salary,
                salary.BaseSalary,
                min,
                max,
                salary.OwedSalary));
        }

        employees.Sort((a, b) =>
        {
            var byJob = string.Compare(a.JobTitle, b.JobTitle, StringComparison.CurrentCulture);
            return byJob != 0 ? byJob : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });

        // Newest first — the head reads the log top-down.
        var log = new List<PayrollLogEntry>(console.Comp.Log);
        log.Reverse();

        _pendingRefresh.Remove(console.Owner);

        _ui.SetUiState(console.Owner, PayrollConsoleUiKey.Key, new PayrollConsoleUiState(
            fundName,
            fundBalance,
            employees,
            bonusPool,
            GetPayableBonus(bonusPool, fundBalance),
            _cfg.GetCVar(IS14CVars.PayrollMaxFine),
            log,
            status));
    }

    private string GetEmployeeName(EntityUid employee, JobSalaryComponent salary)
    {
        if (TryGetCard(salary, out var card)
            && TryComp<IdCardComponent>(card, out var id)
            && !string.IsNullOrWhiteSpace(id.FullName))
        {
            return id.FullName;
        }

        return Name(employee);
    }

    private string GetJobTitle(JobSalaryComponent salary)
    {
        if (TryGetCard(salary, out var card)
            && TryComp<IdCardComponent>(card, out var id)
            && !string.IsNullOrWhiteSpace(id.LocalizedJobTitle))
        {
            return id.LocalizedJobTitle;
        }

        if (salary.JobProtoId != null && _prototypes.TryIndex<JobPrototype>(salary.JobProtoId, out var job))
            return job.LocalizedName;

        return Loc.GetString("is14-payroll-unknown-job");
    }

    /// <summary>Speaks the payroll decision through the employee's ID card, like salary payments do.</summary>
    private void Notify(JobSalaryComponent salary, string message, bool goodNews)
    {
        if (!TryGetCard(salary, out var card))
            return;

        _audio.PlayPvs(goodNews ? GoodNewsSound : BadNewsSound, card, AudioParams.Default.WithVolume(-2f));
        _chat.TrySendInGameICMessage(card, message, InGameICChatType.Speak, hideChat: false, hideLog: true, ignoreActionBlocker: true);
    }

    private bool TryGetCard(JobSalaryComponent salary, out EntityUid card)
    {
        card = default;
        if (salary.IdCardEntity is not { } uid || !Exists(uid))
            return false;

        card = uid;
        return true;
    }
}

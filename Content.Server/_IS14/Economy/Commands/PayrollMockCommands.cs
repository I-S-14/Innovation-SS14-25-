using System.Linq;
using Content.Server._IS14.Economy.Payroll;
using Content.Server.Access.Components;
using Content.Server.Access.Systems;
using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy;
using Content.Shared.Administration;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.Commands;

/// <summary>
/// Fills a department with test employees. The payroll console walks every entity carrying a
/// <see cref="JobSalaryComponent"/>, so a mock needs no player and no body — by default it is
/// just an ID card on the floor.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class PayrollMockCommand : LocalizedEntityCommands
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NamingSystem _naming = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // The base ID card prototype is abstract; the passenger card is the plainest concrete one.
    // Access does not matter for a mock — only the name and job title written on it.
    private const string CardPrototype = "PassengerIDCard";
    private const string MobPrototype = "MobHuman";
    private const string UniformPrototype = "ClothingUniformJumpsuitColorGrey";
    private const string MockSpecies = "Human";
    private const int MaxCount = 50;

    public override string Command => "payroll_mock";

    public override string Description =>
        "Spawns fake employees paid by a job's department fund, for testing payroll consoles.";

    public override string Help => "Usage: payroll_mock <jobEconomyId> [count=1] [card|mob]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 3)
        {
            shell.WriteError(Help);
            return;
        }

        // Mocks are spawned at the caller's feet, which also decides which station pays them.
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("This command spawns the mocks where you stand — it needs an in-game body.");
            return;
        }

        if (!_prototypes.TryIndex<JobEconomyPrototype>(args[0], out var economy))
        {
            shell.WriteError($"No jobEconomy prototype '{args[0]}'.");
            return;
        }

        var count = 1;
        if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count is < 1 or > MaxCount))
        {
            shell.WriteError($"Count must be between 1 and {MaxCount}.");
            return;
        }

        var asMob = false;
        if (args.Length == 3)
        {
            switch (args[2].ToLowerInvariant())
            {
                case "mob":
                    asMob = true;
                    break;
                case "card":
                    break;
                default:
                    shell.WriteError(Help);
                    return;
            }
        }

        var coords = EntityManager.GetComponent<TransformComponent>(player).Coordinates;
        var station = _station.GetOwningStation(player);

        // jobEconomy IDs mirror job IDs, so the console shows a proper job title even for a bare card.
        var jobTitle = _prototypes.TryIndex<JobPrototype>(economy.ID, out var job) ? job.LocalizedName : economy.ID;

        for (var i = 0; i < count; i++)
        {
            var name = _naming.GetName(MockSpecies, _random.Prob(0.5f) ? Gender.Male : Gender.Female);
            var balance = _random.Next(economy.MinBalance, economy.MaxBalance + 1);
            var account = _bankManager.CreateAccount(balance);

            // The card carries the identity: the console reads the name and job title off it,
            // and salary, bonus and fine announcements are spoken through it.
            var card = EntityManager.SpawnEntity(CardPrototype, coords);

            // The passenger card is a preset one, and presets rewrite the name and job title
            // when jobs are assigned — drop that before writing the mock's own identity.
            EntityManager.RemoveComponent<PresetIdCardComponent>(card);

            _idCard.TryChangeFullName(card, name);
            _idCard.TryChangeJobTitle(card, jobTitle);
            EntityManager.EnsureComponent<BankAccountHolderComponent>(card).AccountNumber = account.AccountNumber;

            // A body is only worth spawning when the test needs one — it also counts towards
            // the Gosplan crew metrics, which a loose card deliberately does not.
            var employee = card;
            if (asMob)
            {
                employee = EntityManager.SpawnEntity(MobPrototype, coords);
                _metaData.SetEntityName(employee, name);

                // The ID slot depends on a jumpsuit, so the dummy has to be dressed before it can
                // hold a card. A failed equip is harmless: the card lands at their feet and the
                // console still reads the name off it.
                _inventory.TryEquip(employee, EntityManager.SpawnEntity(UniformPrototype, coords), "jumpsuit", silent: true);
                _inventory.TryEquip(employee, card, "id", silent: true);
            }

            var salary = EntityManager.EnsureComponent<JobSalaryComponent>(employee);
            salary.AccountNumber = account.AccountNumber;
            salary.Salary = economy.Salary;
            salary.BaseSalary = economy.Salary;
            salary.JobProtoId = economy.ID;
            salary.SalaryIntervalSeconds = economy.SalaryIntervalSeconds;
            salary.NextPaymentTime = _timing.CurTime + TimeSpan.FromSeconds(economy.SalaryIntervalSeconds);
            salary.IdCardEntity = card;
            salary.Station = station;
            salary.PayerAccount = economy.PayerAccount;

            var mock = EntityManager.EnsureComponent<PayrollMockComponent>(employee);
            mock.AccountNumber = account.AccountNumber;
            mock.Card = asMob ? card : null;

            shell.WriteLine(
                $"{name} ({jobTitle}) | account #{account.AccountNumber} | balance {balance} cr | " +
                $"salary {economy.Salary} cr / {economy.SalaryIntervalSeconds}s");
        }

        shell.WriteLine($"Spawned {count} mock(s) paid by {economy.PayerAccount.Id}. Clear them with payroll_mock_clear.");

        // Without a station the fund is never found, so salaries would be paid out of thin air
        // and the console would not list them — worth saying out loud rather than debugging later.
        if (station == null)
            shell.WriteError("You are not standing on a station: these mocks belong to no fund and their salary is paid without a debit.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                _prototypes.EnumeratePrototypes<JobEconomyPrototype>().Select(p => p.ID).Order(),
                "<jobEconomyId>"),
            2 => CompletionResult.FromHint("[count=1]"),
            3 => CompletionResult.FromHintOptions(new[] { "card", "mob" }, "[card|mob]"),
            _ => CompletionResult.Empty,
        };
    }
}

/// <summary>Removes every mock employee together with the accounts created for them.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class PayrollMockClearCommand : LocalizedEntityCommands
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;

    public override string Command => "payroll_mock_clear";
    public override string Description => "Deletes every mock employee and the bank accounts created for them.";
    public override string Help => "Usage: payroll_mock_clear";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var mocks = new List<(EntityUid Uid, PayrollMockComponent Comp)>();

        var query = EntityManager.EntityQueryEnumerator<PayrollMockComponent>();
        while (query.MoveNext(out var uid, out var comp))
            mocks.Add((uid, comp));

        foreach (var (uid, comp) in mocks)
        {
            _bankManager.RemoveAccount(comp.AccountNumber);

            // Deleting a body takes its inventory along, but a card that was dropped or
            // pickpocketed outlives its owner and has to go explicitly.
            if (comp.Card is { } card && !EntityManager.Deleted(card))
                EntityManager.QueueDeleteEntity(card);

            EntityManager.QueueDeleteEntity(uid);
        }

        shell.WriteLine($"Cleared {mocks.Count} mock employee(s).");
    }
}

using Content.Server.Access.Systems;
using Content.Server.Administration;
using Content.Shared._IS14.Economy;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class BankAssignCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public string Command => "bank_assign";
    public string Description => "Creates a bank account and salary for an entity.";
    public string Help => "Usage: bank_assign <entityUid> <balance> <salary> [salaryIntervalSeconds=600]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            shell.WriteError(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity) || !_entity.TryGetEntity(netEntity, out var entity))
        {
            shell.WriteError("Invalid or unknown entity UID.");
            return;
        }

        if (!int.TryParse(args[1], out var balance) || balance < 0)
        {
            shell.WriteError("Balance must be a non-negative integer.");
            return;
        }

        if (!int.TryParse(args[2], out var salary) || salary < 0)
        {
            shell.WriteError("Salary must be a non-negative integer.");
            return;
        }

        var salaryInterval = 600;
        if (args.Length == 4 && (!int.TryParse(args[3], out salaryInterval) || salaryInterval <= 0))
        {
            shell.WriteError("Salary interval must be a positive integer (seconds).");
            return;
        }

        var bankManager = _entity.System<BankManagerSystem>();
        var idCardSystem = _entity.System<IdCardSystem>();

        var account = bankManager.CreateAccount(balance);

        EntityUid? idCardUid = null;
        if (idCardSystem.TryFindIdCard(entity.Value, out var idCard))
        {
            var holder = _entity.EnsureComponent<BankAccountHolderComponent>(idCard.Owner);
            holder.AccountNumber = account.AccountNumber;
            idCardUid = idCard.Owner;
        }

        var salaryComp = _entity.EnsureComponent<JobSalaryComponent>(entity.Value);
        salaryComp.AccountNumber = account.AccountNumber;
        salaryComp.Salary = salary;
        salaryComp.SalaryIntervalSeconds = salaryInterval;
        salaryComp.NextPaymentTime = _timing.CurTime + TimeSpan.FromSeconds(salaryInterval);
        salaryComp.IdCardEntity = idCardUid;

        shell.WriteLine($"Account #{account.AccountNumber} (PIN: {account.Pin}) assigned to {_entity.ToPrettyString(entity.Value)}");
        shell.WriteLine($"Balance: {balance} cr | Salary: {salary} cr every {salaryInterval}s");

        if (idCardUid == null)
            shell.WriteLine("Warning: no ID card found on entity — account not bound to ID card.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}

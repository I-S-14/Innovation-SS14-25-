using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._IS14.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class BankSalaryNowCommand : LocalizedEntityCommands
{
    [Dependency] private readonly SalarySystem _salary = default!;

    public override string Command => "bank_salary_now";
    public override string Description => "Immediately pays salary to all crew members, resetting their timers.";
    public override string Help => "Usage: bank_salary_now";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _salary.PayAllSalariesNow();
        shell.WriteLine("Salary paid to all crew members.");
    }
}

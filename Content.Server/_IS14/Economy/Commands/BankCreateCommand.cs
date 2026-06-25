using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._IS14.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class BankCreateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;

    public override string Command => "bank_create";
    public override string Description => "Creates a bank account with the specified starting balance.";
    public override string Help => "Usage: bank_create <balance>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var balance) || balance < 0)
        {
            shell.WriteError(Help);
            return;
        }

        var account = _bankManager.CreateAccount(balance);
        shell.WriteLine($"Account #{account.AccountNumber} created | PIN: {account.Pin} | Balance: {account.Balance} cr");
    }
}

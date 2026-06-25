using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._IS14.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class BankInfoCommand : LocalizedEntityCommands
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;

    public override string Command => "bank_info";
    public override string Description => "Returns information about a bank account.";
    public override string Help => "Usage: bank_info <accountNumber>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var accountNumber))
        {
            shell.WriteError(Help);
            return;
        }

        var account = _bankManager.GetAccount(accountNumber);
        if (account == null)
        {
            shell.WriteError($"Account #{accountNumber} not found.");
            return;
        }

        shell.WriteLine($"Account #{account.AccountNumber} | PIN: {account.Pin} | Balance: {account.Balance} cr");
    }
}

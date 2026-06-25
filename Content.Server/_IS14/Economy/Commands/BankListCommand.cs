using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._IS14.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class BankListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;

    public override string Command => "bank_list";
    public override string Description => "Lists all existing bank accounts.";
    public override string Help => "Usage: bank_list";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var accounts = _bankManager.GetAllAccounts();
        if (accounts.Count == 0)
        {
            shell.WriteLine("No bank accounts exist.");
            return;
        }

        shell.WriteLine($"Total accounts: {accounts.Count}");
        foreach (var account in accounts)
        {
            shell.WriteLine($"  #{account.AccountNumber} | PIN: {account.Pin} | Balance: {account.Balance} cr");
        }
    }
}

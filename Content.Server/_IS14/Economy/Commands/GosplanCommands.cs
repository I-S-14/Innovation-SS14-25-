using Content.Server._IS14.Economy.Gosplan;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._IS14.Economy.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class GosplanEvaluateCommand : LocalizedEntityCommands
{
    [Dependency] private readonly GosplanSystem _gosplan = default!;

    public override string Command => "gosplan_evaluate";
    public override string Description => "Immediately scores the current plan period and pays out, starting a new period.";
    public override string Help => "Usage: gosplan_evaluate";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _gosplan.EvaluateNow();
        shell.WriteLine("Plan period scored.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class GosplanStatusCommand : LocalizedEntityCommands
{
    [Dependency] private readonly GosplanSystem _gosplan = default!;

    public override string Command => "gosplan_status";
    public override string Description => "Prints current plan fulfillment for every station.";
    public override string Help => "Usage: gosplan_status";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(_gosplan.GetStatusReport());
    }
}

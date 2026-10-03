// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Shared._IS14.Research;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Research.Commands;

/// <summary>
/// Reads and tops up the station's research data. Exists for testing and for admin events —
/// the currencies are meant to be earned.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class IS14ResearchPointsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public string Command => "is14_research_points";
    public string Description => "Shows or grants the station's IS14 research data.";
    public string Help => "Usage: is14_research_points [<pointType> <amount>]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("This command needs an attached entity to find the station.");
            return;
        }

        var pointSystem = _entity.System<IS14ResearchPointSystem>();

        if (pointSystem.ResolveStation(player) is not { } station)
        {
            shell.WriteError("Could not resolve a station for your position.");
            return;
        }

        if (args.Length == 0)
        {
            foreach (var type in _proto.EnumeratePrototypes<ResearchPointTypePrototype>())
            {
                shell.WriteLine($"{type.ID}: {pointSystem.GetPoints(station, type.ID)}");
            }

            return;
        }

        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!_proto.HasIndex<ResearchPointTypePrototype>(args[0]))
        {
            shell.WriteError($"Unknown research point type '{args[0]}'.");
            return;
        }

        if (!int.TryParse(args[1], out var amount))
        {
            shell.WriteError("Amount must be an integer.");
            return;
        }

        pointSystem.AddPoints(station, args[0], amount);
        shell.WriteLine($"{args[0]}: {pointSystem.GetPoints(station, args[0])}");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return CompletionResult.FromHintOptions(
            _proto.EnumeratePrototypes<ResearchPointTypePrototype>().Select(p => p.ID),
            "<pointType>");
    }
}

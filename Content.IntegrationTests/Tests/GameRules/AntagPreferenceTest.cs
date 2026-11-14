// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server.Antag;
using Content.Shared.Antag.Components;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests.GameRules;

// Once upon a time, players in the lobby weren't ever considered eligible for antag roles.
// Lets not let that happen again.
[TestFixture]
[Category("GameRuleTests")]
public sealed class AntagPreferenceTest
{
    [Test]
    public async Task TestLobbyPlayersValid()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true
        });

        var server = pair.Server;
        var client = pair.Client;
        var ticker = server.System<ServerGameTicker>();
        var sys = server.System<ServerAntagSelectionSystem>();

        // Initially in the lobby
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(client.AttachedEntity, Is.Null);
        Assert.That(ticker.PlayerGameStatuses[client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        EntityUid uid = default;
        await server.WaitPost(() => uid = server.EntMan.Spawn("Traitor"));
        var rule = new Entity<AntagSelectionComponent>(uid, server.EntMan.GetComponent<AntagSelectionComponent>(uid));
        var def = rule.Comp.Antags.Single().Proto;

        // IsSessionValid & IsEntityValid are preference agnostic and should always be true for players in the lobby.
        // Though maybe that will change in the future, but then GetPlayerPool() needs to be updated to reflect that.
        Assert.That(pair.Player, Is.Not.Null);
        Assert.That(sys.IsSessionValid(pair.Player!, rule, def), Is.True);
        Assert.That(sys.IsEntityValid(client.AttachedEntity, def), Is.True);

        // By default, traitor/antag preferences are disabled, so the pool should be empty.
        var sessions = new List<ICommonSession> { pair.Player! };
        var pool = sessions.Where(session => sys.CanBeAntag(session, rule, def)).ToList();
        Assert.That(pool.Count, Is.EqualTo(0));

        // Opt into the traitor role.
        await pair.SetAntagPreference("Traitor", true);

        Assert.That(sys.IsSessionValid(pair.Player, rule, def), Is.True);
        Assert.That(sys.IsEntityValid(client.AttachedEntity, def), Is.True);
        pool = sessions.Where(session => sys.CanBeAntag(session, rule, def)).ToList();
        Assert.That(pool.Count, Is.EqualTo(1));
        var picked = pair.Server.ResolveDependency<IRobustRandom>().PickAndTake(pool);
        Assert.That(picked, Is.EqualTo(pair.Player));
        Assert.That(sessions.Count, Is.EqualTo(1));

        // opt back out
        await pair.SetAntagPreference("Traitor", false);

        Assert.That(sys.IsSessionValid(pair.Player, rule, def), Is.True);
        Assert.That(sys.IsEntityValid(client.AttachedEntity, def), Is.True);
        pool = sessions.Where(session => sys.CanBeAntag(session, rule, def)).ToList();
        Assert.That(pool.Count, Is.EqualTo(0));

        await server.WaitPost(() => server.EntMan.DeleteEntity(uid));
        await pair.CleanReturnAsync();
    }
}

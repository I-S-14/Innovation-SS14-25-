using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles.Jobs;
using Content.Shared.Spawners.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._IS14;

[Category("MapTests")]
public sealed class RetainedStationRoundStartTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [TestCase("Atlas", 0)]
    [TestCase("Relic", 0)]
    [TestCase("Atlas", 60)]
    [TestCase("Relic", 60)]
    public async Task RetainedStationStartsRoundAndSpawnsPlayerAsMilitia(string map, int gameSeconds)
    {
        var ticker = Server.System<ServerGameTicker>();
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameMap, map);
            ticker.SetGamePreset("Sandbox");
            Server.ResolveDependency<IGameMapManager>().SelectMap(map);
        });
        await Pair.SetJobPriorities(("Passenger", JobPriority.Never), ("JobMilitia", JobPriority.High));
        await Server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await Pair.RunTicksSync(10);
        EntityUid? spawnedPlayer = null;
        await Server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(Server.ResolveDependency<IGameMapManager>().GetSelectedMap()?.ID, Is.EqualTo(map));
            var user = Client.User!.Value;
            Assert.That(ticker.PlayerGameStatuses[user], Is.EqualTo(PlayerGameStatus.JoinedGame));
            var player = Server.PlayerMan.SessionsDict[user].AttachedEntity;
            spawnedPlayer = player;
            Assert.That(player, Is.Not.Null);
            Assert.That(SEntMan.EntityExists(player), Is.True);
            var mind = Server.System<MindSystem>().GetMind(player!.Value);
            Assert.That(Server.System<SharedJobSystem>().MindTryGetJobId(mind, out var job), Is.True);
            Assert.That(job?.Id, Is.EqualTo("JobMilitia"));
            Assert.That(Server.System<ServerStationSystem>().GetOwningStation(player.Value), Is.Not.Null);
            var transform = Server.System<SharedTransformSystem>();
            var position = transform.GetWorldPosition(player.Value);
            var markers = SEntMan.EntityQueryEnumerator<SpawnPointComponent>();
            var atSecurityMarker = false;
            while (markers.MoveNext(out var marker, out var point))
            {
                if (point.Job?.Id == "SecurityOfficer"
                    && (transform.GetWorldPosition(marker) - position).LengthSquared() < 0.01f)
                    atSecurityMarker = true;
            }
            Assert.That(atSecurityMarker, Is.True, "Militia must spawn at its department marker rather than a random fallback.");
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(player.Value, "id", out var id), Is.True);
            Assert.That(SEntMan.EntityExists(id), Is.True);
        });
        Assert.That(Client.AttachedEntity, Is.Not.Null);
        if (gameSeconds > 0)
        {
            var tickRate = Server.ResolveDependency<IGameTiming>().TickRate;
            for (var elapsed = 0; elapsed < gameSeconds; elapsed += 5)
                await Pair.RunTicksSync(5 * tickRate);

            await Server.WaitAssertion(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
                Assert.That(Server.PlayerMan.SessionsDict[Client.User!.Value].AttachedEntity, Is.EqualTo(spawnedPlayer));
                Assert.That(SEntMan.EntityExists(spawnedPlayer), Is.True);
                Assert.That(SEntMan.GetComponent<MobStateComponent>(spawnedPlayer!.Value).CurrentState, Is.EqualTo(MobState.Alive),
                    "The player must survive an idle minute in their mapped department with its real atmosphere and body systems.");
            });
            Assert.That(Client.AttachedEntity, Is.Not.Null);
        }
        await Server.WaitPost(() => ticker.RestartRound());
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby)));
        // Dirty disposal isolates the loaded maps and round services from later tests.
    }
}

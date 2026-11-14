using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Species.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DionaReformCompatibilityTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ReformKeepsTheMindSpeciesAndContainer(bool contained)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid nymph = default, mind = default, crate = default, reformed = default, observer = default;
        ICommonSession session = null!;
        EntityUid? previous = null;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            moles[(int) Gas.CarbonDioxide] = 1f;
            server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false,
                new GasMixture(moles, Atmospherics.T20C));
            for (var x = 1; x <= 3; x++)
                server.System<SharedMapSystem>().SetTile(map.Grid.Owner, map.Grid.Comp,
                    map.GridCoords.Offset(new Vector2(x, 0)), map.Tile.Tile);
            var coords = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));
            nymph = em.SpawnEntity("MobDionaNymph", coords);
            // This fixture has no player controlling the nymph; don't let its NPC wander cancel the do-after.
            em.RemoveComponent<HTNComponent>(nymph);
            mind = server.System<SharedMindSystem>().CreateMind(null, "Upstream diona test").Owner;
            server.System<SharedMindSystem>().TransferTo(mind, nymph);
            if (contained)
            {
                crate = em.SpawnEntity("CrateGenericSteel", coords);
                var containers = server.System<SharedContainerSystem>();
                Assert.That(containers.Insert(nymph, containers.GetContainer(crate, "entity_storage")), Is.True);
            }
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            observer = em.SpawnEntity("MobObserver", coords);
            server.PlayerMan.SetAttachedEntity(session, observer);
            var reform = em.GetComponent<ReformComponent>(nymph);
            Assert.That(reform.ReformTime, Is.EqualTo(10f));
            Assert.That(reform.StartDelayed, Is.True);
            Assert.That(reform.ReformPrototype.Id, Is.EqualTo("MobDionaReformed"));
            var actionUid = reform.ActionEntity!.Value;
            var action = em.GetComponent<ActionComponent>(actionUid);
            Assert.That(action.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(600)));
            Assert.That(action.Cooldown, Is.Not.Null);
            // Exercise the real reform sequence after its configured initial ten-minute cooldown.
            var actions = server.System<SharedActionsSystem>();
            actions.RemoveCooldown((actionUid, action));
            actions.PerformAction((nymph, em.GetComponent<ActionsComponent>(nymph)), (actionUid, action));
        });
        await pair.RunSeconds(9);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(nymph), Is.True,
            "Reform must wait for the retained ten-second do-after."));
        await pair.RunSeconds(2);
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(nymph), Is.False);
            reformed = em.EntityQuery<MetaDataComponent>().Single(c => c.EntityPrototype?.ID == "MobDionaReformed").Owner;
            Assert.That(em.GetComponent<MindComponent>(mind).CurrentEntity, Is.EqualTo(reformed));
            Assert.That(em.GetComponent<MindComponent>(mind).CharacterName, Is.EqualTo("Upstream diona test"));
            Assert.That(em.GetComponent<HumanoidAppearanceComponent>(reformed).Species.Id, Is.EqualTo("Diona"));
            Assert.That(server.System<MobStateSystem>().IsAlive(reformed), Is.True);
            var deadIc = new GetCharactedDeadIcEvent(null);
            em.EventBus.RaiseLocalEvent(reformed, ref deadIc);
            Assert.That(deadIc.Dead, Is.True, "Reformed diona retain the existing objective/round-end death marker.");
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainingContainer((reformed, null, null), out var container), Is.EqualTo(contained));
            var visible = map.GridCoords.Offset(new Vector2(2.5f, 0.5f));
            if (contained)
            {
                Assert.That(container!.Owner, Is.EqualTo(crate));
                // Extract only for the subsequent client appearance check; opening/locking is not under test.
                Assert.That(containers.Remove((reformed, null, null), container, force: true, destination: visible), Is.True);
            }
            else
                server.System<SharedTransformSystem>().SetCoordinates(reformed, visible);
        });
        await pair.RunTicksSync(10);
        var clientDiona = pair.ToClientUid(reformed);
        await pair.Client.WaitAssertion(() =>
            Assert.That(pair.Client.EntMan.GetComponent<HumanoidAppearanceComponent>(clientDiona).Species.Id, Is.EqualTo("Diona")));
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
            server.EntMan.DeleteEntity(mind);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}

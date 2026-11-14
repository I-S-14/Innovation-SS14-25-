using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared.NPC.Components;
using Content.Shared.Spider;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ClownSpiderWebCompatibilityTest
{
    [Test]
    public async Task ClownSpiderCreatesClownWebsWithoutDuplicatingExistingWebs()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid spider = default;
        List<EntityUid> webs = new();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            foreach (var offset in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY })
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords.Offset(offset), map.Tile.Tile);
            spider = em.SpawnEntity("MobClownSpider", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            // Exercise the controlled spider's manual ability on one fixed tile;
            // autonomous wandering can legitimately expose new tiles between casts.
            em.RemoveComponent<HTNComponent>(spider);
            Assert.That(em.GetComponent<NpcFactionMemberComponent>(spider).Factions.Select(faction => faction.Id),
                Does.Contain("Xeno"));
            var action = new SpiderWebActionEvent { Performer = spider };
            em.EventBus.RaiseLocalEvent(spider, action);
            Assert.That(action.Handled, Is.True);
            var query = em.EntityQueryEnumerator<SpiderWebObjectComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (em.GetComponent<TransformComponent>(uid).GridUid != map.GridCoords.EntityId)
                    continue;
                webs.Add(uid);
                Assert.That(em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID, Is.EqualTo("SpiderWebClown"));
            }
            Assert.That(webs, Is.Not.Empty);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var repeated = new SpiderWebActionEvent { Performer = spider };
            server.EntMan.EventBus.RaiseLocalEvent(spider, repeated);
            Assert.That(repeated.Handled, Is.False);
            foreach (var web in webs)
                server.EntMan.DeleteEntity(web);
            server.EntMan.DeleteEntity(spider);
        });
        await pair.CleanReturnAsync();
    }
}

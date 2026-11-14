using System.Linq;
using System.Numerics;
using Content.Shared.Shuttles.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedFriendlyShuttleMapTest
{
    [TestCase("lost_cargo")]
    [TestCase("traveling_china_cuisine")]
    [TestCase("disaster_evacpod")]
    [TestCase("cruiser")]
    [TestCase("cryptid")]
    [TestCase("eternal")]
    [TestCase("flatline")]
    [TestCase("gym")]
    [TestCase("incorporation")]
    [TestCase("joe")]
    [TestCase("lambordeere")]
    [TestCase("meatzone")]
    [TestCase("microshuttle")]
    [TestCase("spacebus")]
    public async Task RetainedShuttleInitializesAndCanBeViewedByTheClient(string filename)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        MapId mapId = default;
        EntityUid gridUid = default, observer = default;
        EntityUid? previous = null;
        ICommonSession session = null!;
        Vector2[] viewpoints = [];
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            server.System<SharedMapSystem>().CreateMap(out mapId);
            Assert.That(server.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath("Maps/Shuttles/ShuttleEvent/" + filename + ".yml"), out var grid), Is.True);
            gridUid = grid!.Value.Owner;
            Assert.That(em.HasComponent<ShuttleComponent>(gridUid), Is.True);
            var bounds = grid.Value.Comp.LocalAABB;
            viewpoints = [bounds.Center,
                new Vector2(bounds.Left + 1, bounds.Bottom + 1),
                new Vector2(bounds.Right - 1, bounds.Top - 1),
                new Vector2(bounds.Left + 1, bounds.Top - 1),
                new Vector2(bounds.Right - 1, bounds.Bottom + 1)];
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            observer = em.SpawnEntity("MobObserver", new EntityCoordinates(gridUid, bounds.Center));
            server.PlayerMan.SetAttachedEntity(session, observer);
        });
        await pair.RunTicksSync(10);
        var clientGrid = pair.ToClientUid(gridUid);
        await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan.EntityExists(clientGrid), Is.True));
        foreach (var position in viewpoints)
        {
            await server.WaitPost(() => server.System<SharedTransformSystem>()
                .SetCoordinates(observer, new EntityCoordinates(gridUid, position)));
            await pair.RunTicksSync(5);
        }
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.EntMan.DeleteEntity(observer);
            server.System<SharedMapSystem>().DeleteMap(mapId);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}

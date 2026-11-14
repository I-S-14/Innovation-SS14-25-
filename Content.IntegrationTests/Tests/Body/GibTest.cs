// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Clothing.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Body;

[TestFixture]
public sealed class GibTest
{
    [Test]
    public async Task TestGib()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var (server, client) = (pair.Server, pair.Client);
        var map = await pair.CreateTestMap();

        EntityUid target1 = default;
        EntityUid target2 = default;

        await server.WaitAssertion(() => target1 = server.EntMan.Spawn("MobHuman", map.MapCoords));
        await server.WaitAssertion(() => target2 = server.EntMan.Spawn("MobHuman", map.MapCoords));
        await pair.WaitCommand($"setoutfit {server.EntMan.GetNetEntity(target1)} CaptainGear");
        await pair.WaitCommand($"setoutfit {server.EntMan.GetNetEntity(target2)} CaptainGear");

        await pair.RunTicksSync(5);
        var nuid1 = pair.ToClientUid(target1);
        var nuid2 = pair.ToClientUid(target2);
        Assert.That(client.EntMan.EntityExists(nuid1));
        Assert.That(client.EntMan.EntityExists(nuid2));

        await server.WaitAssertion(() =>
        {
            foreach (var (target, organs) in new[] { (target1, false), (target2, true) })
            {
                var dropped = server.System<BodySystem>().GibBody(target, gibOrgans: organs);
                Assert.That(dropped, Is.Not.Empty);
                Assert.That(dropped.Any(uid => server.EntMan.HasComponent<ClothingComponent>(uid)), Is.True,
                    "Real equipped items must still be dropped.");
                Assert.That(dropped.Any(uid => server.EntMan.HasComponent<ContainedSolutionComponent>(uid)), Is.False,
                    "Internal solution entities must stay with their owner.");
            }
        });

        await pair.RunTicksSync(5);
        await pair.WaitCommand("dirty");
        await pair.RunTicksSync(5);

        Assert.That(!client.EntMan.EntityExists(nuid1));
        Assert.That(!client.EntMan.EntityExists(nuid2));

        await pair.CleanReturnAsync();
    }
}

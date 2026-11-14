using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyButcheringTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestButcherKnifeUser
  components:
  - type: Sharp
  - type: DoAfter
- type: entity
  id: IS14TestButcherProduct
- type: entity
  id: IS14TestKnifeButcherTarget
  components:
  - type: Butcherable
    butcheringType: Knife
    butcherDelay: 0.1
    spawned:
    - id: IS14TestButcherProduct
      amount: 2
- type: entity
  parent: IS14TestKnifeButcherTarget
  id: IS14TestSpikeButcherTarget
  components:
  - type: Butcherable
    butcheringType: Spike
";

    [TestCase("IS14TestKnifeButcherTarget", true)]
    [TestCase("IS14TestSpikeButcherTarget", false)]
    public async Task KnifeRespectsToolTypeAndProducesConfiguredItems(string prototype, bool canButcher)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid knife = default;
        EntityUid target = default;
        await server.WaitAssertion(() =>
        {
            knife = server.EntMan.SpawnEntity("IS14TestButcherKnifeUser", map.GridCoords);
            target = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var interact = new AfterInteractEvent(knife, knife, target, map.GridCoords, true);
            server.EntMan.EventBus.RaiseLocalEvent(knife, interact);
            Assert.That(interact.Handled, Is.EqualTo(canButcher));
        });
        if (canButcher)
            await PoolManager.WaitUntil(server, () => !server.EntMan.EntityExists(target));
        else
            await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(target), Is.EqualTo(!canButcher));
            var products = new System.Collections.Generic.List<EntityUid>();
            var query = server.EntMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var metadata))
            {
                if (metadata.EntityPrototype?.ID == "IS14TestButcherProduct")
                    products.Add(uid);
            }
            Assert.That(products, Has.Count.EqualTo(canButcher ? 2 : 0));
            foreach (var product in products)
                server.EntMan.DeleteEntity(product);
            if (server.EntMan.EntityExists(target))
                server.EntMan.DeleteEntity(target);
            server.EntMan.DeleteEntity(knife);
        });
        await pair.CleanReturnAsync();
    }
}

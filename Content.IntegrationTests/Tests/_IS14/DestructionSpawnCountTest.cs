using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DestructionSpawnCountTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestIS14DestructionSpawnCount
  parent: [Crowbar, BaseItemDestructible]
  components:
  - type: Destructible
    thresholds:
    - trigger: !type:DamageTrigger
        damage: 1
      behaviors:
      - !type:SpawnEntitiesBehavior
        spawn:
          Wrench: {min: 1.5, max: 1.5}
          SheetSteel1: {min: 1.5, max: 1.5}
      - !type:DoActsBehavior
        acts: [Destruction]
";

    [Test]
    public async Task StackAndIndividualDropsUseTheSameIntegerCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var probe = em.SpawnEntity("TestIS14DestructionSpawnCount", map.GridCoords);
            server.System<DamageableSystem>().TryChangeDamage(probe,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
            var steel = em.EntityQuery<StackComponent>().Where(c => c.StackTypeId.Id == "Steel").Sum(c => c.Count);
            Assert.That(steel, Is.EqualTo(1));
            Assert.That(em.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == "Wrench"), Is.EqualTo(1),
                "An individual entity count must be truncated like a stack count, rather than rounded up by the loop.");
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}

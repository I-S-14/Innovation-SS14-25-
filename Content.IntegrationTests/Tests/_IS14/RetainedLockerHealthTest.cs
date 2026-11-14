using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedLockerHealthTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestIS14RegularLockerHealth
  parent: LockerSecurity
  components:
  - type: ContainerFill
    containers:
      entity_storage: [Wrench]

- type: entity
  id: TestIS14SecureLockerHealth
  parent: LockerBaseSecure
  components:
  - type: ContainerFill
    containers:
      entity_storage: [Wrench]
";

    [TestCase("TestIS14RegularLockerHealth", 60)]
    [TestCase("TestIS14SecureLockerHealth", 150)]
    public async Task LockerKeepsItsThresholdAndReleasesStoredItems(string prototype, int threshold)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid locker = default, wrench = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            locker = em.SpawnEntity(prototype, map.GridCoords);
            var contents = server.System<SharedContainerSystem>().GetContainer(locker, "entity_storage").ContainedEntities;
            Assert.That(contents, Has.Count.EqualTo(1));
            wrench = contents.Single();
            Assert.That(em.GetComponent<MetaDataComponent>(wrench).EntityPrototype!.ID, Is.EqualTo("Wrench"));
            server.System<DamageableSystem>().TryChangeDamage(locker,
                new DamageSpecifier { DamageDict = { ["Blunt"] = threshold - 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(locker), Is.True,
                "A parent's earlier destruction threshold must not delete the locker.");
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(locker), Is.EqualTo(FixedPoint2.New(threshold - 1)));
            Assert.That(server.System<SharedContainerSystem>().GetContainer(locker, "entity_storage").ContainedEntities,
                Does.Contain(wrench));
            server.System<DamageableSystem>().TryChangeDamage(locker,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(locker), Is.False);
            Assert.That(em.EntityExists(wrench), Is.True, "Destruction must not delete the stored item.");
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(wrench), Is.False);
            var metal = em.EntityQuery<StackComponent>().Where(c => c.StackTypeId.Id == "Steel").Sum(c => c.Count);
            Assert.That(metal, Is.InRange(1, 2));
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}

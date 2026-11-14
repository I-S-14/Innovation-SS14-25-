using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedBluespaceCrateHealthTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestIS14RegularSecureCrateHealth
  parent: CrateSecure
  components:
  - type: ContainerFill
    containers:
      entity_storage: [Wrench]

- type: entity
  id: TestIS14BluespaceCrateHealth
  parent: CrateBaseBluespace
  components:
  - type: ContainerFill
    containers:
      entity_storage: [Wrench]
";

    [TestCase("TestIS14RegularSecureCrateHealth", 60, 0)]
    [TestCase("TestIS14BluespaceCrateHealth", 100, 1)]
    public async Task SecureCratePreservesItsHealthContentsAndLifeline(string prototype, int threshold, int lifelines)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid crate = default, wrench = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            crate = em.SpawnEntity(prototype, map.GridCoords);
            wrench = server.System<SharedContainerSystem>().GetContainer(crate, "entity_storage").ContainedEntities.Single();
            Assert.That(em.GetComponent<MetaDataComponent>(wrench).EntityPrototype!.ID, Is.EqualTo("Wrench"));
            server.System<DamageableSystem>().TryChangeDamage(crate,
                new DamageSpecifier { DamageDict = { ["Blunt"] = threshold - 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(crate), Is.True);
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(crate), Is.EqualTo(FixedPoint2.New(threshold - 1)));
            Assert.That(server.System<SharedContainerSystem>().GetContainer(crate, "entity_storage").ContainedEntities, Does.Contain(wrench));
            server.System<DamageableSystem>().TryChangeDamage(crate,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(crate), Is.False);
            Assert.That(em.EntityExists(wrench), Is.True);
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(wrench), Is.False);
            Assert.That(em.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == "BluespaceLifeline"), Is.EqualTo(lifelines));
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}

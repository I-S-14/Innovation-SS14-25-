#nullable enable annotations
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedMeteorTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestMeteorTarget
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      target:
        shape: !type:PhysShapeAabb
          bounds: '-0.4,-0.4,0.4,0.4'
        hard: true
        layer: [Impassable]
  - type: Damageable
    damageContainer: Inorganic
  - type: Destructible
    thresholds:
    - trigger: !type:DamageTrigger
        damage: 25
      behaviors:
      - !type:DoActsBehavior
        acts: [Destruction]
";

    [TestCase("MeteorSpaceDust", null, null, 0)]
    [TestCase("MeteorSmall", null, null, 0)]
    [TestCase("MeteorMedium", null, null, 0)]
    [TestCase("MeteorLarge", null, null, 0)]
    [TestCase("MeteorUrist", "blood", "Blood", 1000)]
    [TestCase("MeteorCow", "milk", "Milk", 500)]
    [TestCase("MeteorClown", "laughter", "Laughter", 500)]
    [TestCase("MeteorPotato", "ketchup", "Ketchup", 100)]
    public async Task MeteorDestroysAnObstacleAndAbsorbsOnlyItsRemainingDurability(
        string prototype, string? solutionName, string? reagent, int quantity)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid meteor = default;
        EntityUid target = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            target = em.SpawnEntity("IS14TestMeteorTarget", map.GridCoords.Offset(new Vector2(3, 0)));
            meteor = em.SpawnEntity(prototype, map.GridCoords);
            if (prototype == "MeteorSmall")
                server.System<DamageableSystem>().TryChangeDamage(target,
                    new Content.Shared.Damage.DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }, true);
            if (solutionName != null)
            {
                Assert.That(server.System<SharedSolutionContainerSystem>()
                    .TryGetSolution(meteor, solutionName, out _, out var solution), Is.True);
                Assert.That(solution!.Volume, Is.EqualTo(FixedPoint2.New(quantity)));
                Assert.That(solution.GetTotalPrototypeQuantity(reagent!), Is.EqualTo(FixedPoint2.New(quantity)));
            }
            server.System<SharedPhysicsSystem>().SetLinearVelocity(meteor, new Vector2(10, 0),
                body: em.GetComponent<PhysicsComponent>(meteor));
        });
        await PoolManager.WaitUntil(server, () => !server.EntMan.EntityExists(target));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(meteor), Is.True);
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(meteor),
                Is.EqualTo(FixedPoint2.New(prototype == "MeteorSmall" ? 15 : 25)));
            server.EntMan.DeleteEntity(meteor);
        });
        await pair.CleanReturnAsync();
    }
}

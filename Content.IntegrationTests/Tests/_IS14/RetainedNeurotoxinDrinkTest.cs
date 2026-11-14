using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedNeurotoxinDrinkTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestIS14NeurotoxinDrinkSpawner
  parent: RandomDrinkGlass
  components:
  - type: RandomSpawner
    chance: 1
    rareChance: 1
    rarePrototypes: [DrinkNeurotoxinGlass]
";

    [Test]
    public async Task RareDrinkSpawnProducesAFullGlassThatCanSupplyADose()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid spawner = default, drink = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            spawner = em.SpawnEntity("TestIS14NeurotoxinDrinkSpawner", map.GridCoords);
            var drinks = em.EntityQuery<MetaDataComponent>().Where(c =>
                c.EntityPrototype?.ID == "DrinkNeurotoxinGlass").ToArray();
            Assert.That(drinks, Has.Length.EqualTo(1));
            drink = drinks[0].Owner;
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(drink, "drink", out var entity, out var solution), Is.True);
            Assert.That(solution!.Volume, Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(solution.MaxVolume, Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(solution.GetTotalPrototypeQuantity("Neurotoxin"), Is.EqualTo(FixedPoint2.New(30)));
            var dose = solutions.SplitSolution(entity!.Value, FixedPoint2.New(5));
            Assert.That(dose.GetTotalPrototypeQuantity("Neurotoxin"), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(solution.GetTotalPrototypeQuantity("Neurotoxin"), Is.EqualTo(FixedPoint2.New(25)));
        });
        await pair.RunTicksSync(3);
        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(drink);
            if (server.EntMan.EntityExists(spawner))
                server.EntMan.DeleteEntity(spawner);
        });
        await pair.CleanReturnAsync();
    }
}

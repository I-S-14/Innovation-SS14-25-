using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedPlantChemistryTest
{
    private static readonly ProtoId<ReagentPrototype> CompatibilityDermalineReagentId = "Dermaline";

    [Test]
    public async Task AloeProduceKeepsOriginalMedicalReagents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var before = Entities(em);
            var plant = em.SpawnEntity("AloePlants", map.GridCoords);
            server.System<BotanySystem>().SpawnProduce(plant, map.GridCoords, 1);
            var created = Entities(em).Except(before).ToArray();
            var aloe = created.Single(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "FoodAloe");
            var produce = em.GetComponent<ProduceComponent>(aloe);
            Assert.That(server.System<SharedSolutionContainerSystem>().TryGetSolution(aloe, produce.TargetSolution, out _, out var solution), Is.True);
            Assert.That(solution!.GetTotalPrototypeQuantity("Aloe"), Is.EqualTo(FixedPoint2.New(2)));
            Assert.That(solution.GetTotalPrototypeQuantity("SilverSulfadiazine"), Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(solution.ContainsPrototype(CompatibilityDermalineReagentId), Is.False);
            foreach (var uid in created)
                if (em.EntityExists(uid))
                    em.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("BlueTomatoPlants", "FoodBlueTomato", "SpaceLube", "Singulo")]
    [TestCase("IS14BluespaceTomatoSeedsPlant", "FoodBluespaceTomato", "Singulo", "SpaceLube")]
    public async Task TomatoSpeciesKeepDistinctProduceAndChemistry(string prototype, string product, string chemical, string otherChemical)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var before = Entities(em);
            var plant = em.SpawnEntity(prototype, map.GridCoords);
            server.System<BotanySystem>().SpawnProduce(plant, map.GridCoords, 1);
            var created = Entities(em).Except(before).ToArray();
            var fruit = created.Single(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == product);
            var produce = em.GetComponent<ProduceComponent>(fruit);
            Assert.That(produce.PlantProtoId?.Id, Is.EqualTo(prototype));
            Assert.That(server.System<SharedSolutionContainerSystem>().TryGetSolution(fruit, produce.TargetSolution, out _, out var solution), Is.True);
            Assert.That(solution!.ContainsPrototype(chemical), Is.True);
            Assert.That(solution.ContainsPrototype(otherChemical), Is.False);
            foreach (var uid in created)
                if (em.EntityExists(uid))
                    em.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }

    private static HashSet<EntityUid> Entities(IEntityManager manager)
    {
        var entities = new HashSet<EntityUid>();
        var query = manager.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out _))
            entities.Add(uid);
        return entities;
    }
}

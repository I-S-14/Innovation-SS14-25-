using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ChemistryBagFillCompatibilityTest
{
    [TestCase("ChemBagFilled", 5)]
    [TestCase("ClothingBeltChemBagSyndiFilled", 15)]
    public async Task RetainedChemistryBagContainsItsJugAndAllStartingEquipment(string prototype, int count)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var bag = em.SpawnEntity(prototype, map.GridCoords);
            var contents = server.System<SharedContainerSystem>().GetContainer(bag, "storagebase").ContainedEntities.ToArray();
            Assert.That(contents, Has.Length.EqualTo(count));
            var jug = contents.Single(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == "Jug");
            Assert.That(server.System<SharedSolutionContainerSystem>().TryGetSolution(jug, "drink", out _, out var solution), Is.True);
            // Preserve the new chemistry volume while retaining Goob/IS14 bag compatibility.
            Assert.That(solution!.MaxVolume, Is.EqualTo(FixedPoint2.New(240)));
            foreach (var item in contents)
                em.DeleteEntity(item);
            em.DeleteEntity(bag);
        });
        await pair.CleanReturnAsync();
    }
}

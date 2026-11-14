using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MedicalBottleSolutionCompatibilityTest
{
    [TestCase("ChemistryBottleTramadol", "Tramadol")]
    [TestCase("ChemistryBottleOxycodone", "Oxycodone")]
    [TestCase("ChemistryBottleIbuprofen", "Ibuprofen")]
    [TestCase("ChemistryBottleNecrosol", "Necrosol")]
    public async Task BottleContainsTheRetainedMedicineAndCanSupplyADose(string prototype, string reagent)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var bottle = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(bottle, "drink", out var entity, out var solution), Is.True);
            Assert.That(solution!.MaxVolume, Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(solution.GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(30)));
            var dose = solutions.SplitSolution(entity!.Value, FixedPoint2.New(5));
            Assert.That(dose.GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(solution.GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(25)));
            server.EntMan.DeleteEntity(bottle);
        });
        await pair.CleanReturnAsync();
    }
}

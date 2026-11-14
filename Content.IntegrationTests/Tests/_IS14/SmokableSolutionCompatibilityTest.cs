#nullable enable annotations
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class SmokableSolutionCompatibilityTest
{
    [TestCase("Cigar", "smokable", 25, true, "Nicotine", 15, null, 0)]
    [TestCase("CigarGoldenGang", "smokable", 35, true, "Nicotine", 15, "Omnizine", 10)]
    [TestCase("CigaretteSyndicate", "smokable", 40, true, "Nicotine", 10, "Omnizine", 30)]
    [TestCase("CigaretteSyndicate", "grinded", 40, true, "Nicotine", 10, "Omnizine", 5)]
    [TestCase("CigaretteOmnizine", "smokable", 40, true, "Nicotine", 10, "Omnizine", 10)]
    [TestCase("Joint", "smokable", 30, true, "THC", 20, null, 0)]
    [TestCase("BluntCigar", "smokable", 30, true, "THC", 20, null, 0)]
    [TestCase("CigaretteSpent", "smokable", 20, true, null, 0, null, 0)]
    [TestCase("CigaretteMedical", "smokable", 10, true, null, 0, null, 0)]
    [TestCase("ECigarette", "smokable", 20, true, null, 0, null, 0)]
    [TestCase("CryostasisCigarette", "smokable", 20, false, null, 0, null, 0)]
    public async Task SpawnedSolutionKeepsItsRecipe(
        string prototype, string solutionId, int capacity, bool canReact,
        string? firstReagent, int firstQuantity, string? secondReagent, int secondQuantity)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var item = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(item, solutionId, out _, out var solution), Is.True);
            Assert.That(solution!.MaxVolume, Is.EqualTo(FixedPoint2.New(capacity)));
            Assert.That(solution.CanReact, Is.EqualTo(canReact));
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(firstQuantity + secondQuantity)));
            if (firstReagent != null)
                Assert.That(solution.GetTotalPrototypeQuantity(firstReagent), Is.EqualTo(FixedPoint2.New(firstQuantity)));
            if (secondReagent != null)
                Assert.That(solution.GetTotalPrototypeQuantity(secondReagent), Is.EqualTo(FixedPoint2.New(secondQuantity)));
            server.EntMan.DeleteEntity(item);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("JointRainbow")]
    [TestCase("BluntRainbow")]
    public async Task RainbowBlendKeepsAllFiveReagents(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var item = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(item, "smokable", out _, out var solution), Is.True);
            Assert.That(solution!.MaxVolume, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(solution.GetTotalPrototypeQuantity("SpaceDrugs"), Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(solution.GetTotalPrototypeQuantity("Lipolicide"), Is.EqualTo(FixedPoint2.New(4)));
            Assert.That(solution.GetTotalPrototypeQuantity("MindbreakerToxin"), Is.EqualTo(FixedPoint2.New(2.66)));
            Assert.That(solution.GetTotalPrototypeQuantity("Happiness"), Is.EqualTo(FixedPoint2.New(2.66)));
            Assert.That(solution.GetTotalPrototypeQuantity("Psicodine"), Is.EqualTo(FixedPoint2.New(0.8)));
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(14.12)));
            server.EntMan.DeleteEntity(item);
        });
        await pair.CleanReturnAsync();
    }
}

#nullable enable annotations
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class IngestionSolutionCompatibilityTest
{
    private static readonly ProtoId<ReagentPrototype> CompatibilityWaterReagentId = "Water";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestIngestionLegacy
  components:
  - type: Edible
    solution: food
  - type: SolutionContainerManager
    solutions:
      food:
        maxVol: 20
        reagents:
        - ReagentId: Water
          Quantity: 7
- type: entity
  id: IS14TestIngestionDirect
  components:
  - type: Edible
    solution: food
  - type: Solution
    id: food
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 7
- type: entity
  id: IS14TestIngestionNamedSolution
  components:
  - type: Solution
    id: food
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 7
- type: entity
  id: IS14TestIngestionModern
  components:
  - type: Edible
    solution: food
  - type: SolutionManager
    solutions: [IS14TestIngestionNamedSolution]
";

    [TestCase("SinkWide", true)]
    [TestCase("IS14TestIngestionLegacy", true)]
    [TestCase("IS14TestIngestionDirect", true)]
    [TestCase("IS14TestIngestionModern", true)]
    [TestCase(null, false)]
    public async Task AccessUsesTheSolutionProvider(string? prototype, bool expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid target = default;
        await server.WaitAssertion(() =>
        {
            user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            target = server.EntMan.SpawnEntity(prototype, map.GridCoords);
        });
        // Removal of the legacy component is deferred until the end of the tick.
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.HasComponent<SolutionContainerManagerComponent>(target), Is.False);
            var result = server.System<IngestionSystem>().CanAccessSolution(target, user, out var solution, out _);
            Assert.That(result, Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(solution, Is.Not.Null);
                Assert.That(solution!.Value.Comp.Solution.ContainsPrototype(CompatibilityWaterReagentId), Is.True);
                Assert.That(solution.Value.Comp.Solution.Volume.Float(), Is.GreaterThan(0f));
            }
            else
                Assert.That(solution, Is.Null);
            em.DeleteEntity(target);
            em.DeleteEntity(user);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("PillTramadol", 20, 7.5, "Tramadol", 7.5)]
    [TestCase("PillIbuprofen", 20, 10, "Ibuprofen", 10)]
    [TestCase("PillOxycodone", 20, 5, "Oxycodone", 5)]
    [TestCase("FoodMango", 9, 9, "Vitamin", 5)]
    [TestCase("FoodMealCactusFruit", 18, 18, "VitriumFroth", 8)]
    [TestCase("FoodMealSerakaCap", 10, 10, "SerakaExtract", 7)]
    [TestCase("EctoplasmGoon", 50, 50, "Coldsauce", 25)]
    public async Task RetainedFoodAndPillsPreserveTheirDoseWhenSplit(
        string prototype, int capacity, double volume, string reagent, double quantity)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var item = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(item, "food", out var solutionEntity, out var contents), Is.True);
            Assert.That(contents!.MaxVolume, Is.EqualTo(FixedPoint2.New(capacity)));
            Assert.That(contents.Volume, Is.EqualTo(FixedPoint2.New(volume)));
            Assert.That(contents.GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(quantity)));
            var half = solutions.SplitSolution(solutionEntity!.Value, FixedPoint2.New(volume / 2));
            Assert.That(half.Volume, Is.EqualTo(FixedPoint2.New(volume / 2)));
            Assert.That(half.GetTotalPrototypeQuantity(reagent), Is.EqualTo(FixedPoint2.New(quantity / 2)));
            Assert.That(contents.Volume, Is.EqualTo(FixedPoint2.New(volume / 2)));
            server.EntMan.DeleteEntity(item);
        });
        await pair.CleanReturnAsync();
    }
}

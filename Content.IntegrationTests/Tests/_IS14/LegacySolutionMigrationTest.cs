using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacySolutionMigrationTest
{
    private static readonly ProtoId<ReagentPrototype> CompatibilityWaterReagentId = "Water";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestLegacyEdibleSolution
  components:
  - type: Edible
  - type: SolutionContainerManager
    solutions:
      food:
        maxVol: 20
        reagents:
        - ReagentId: Water
          Quantity: 7

- type: entity
  parent: IS14TestLegacyEdibleSolution
  id: IS14TestLegacyRegeneration
  components:
  - type: SolutionRegeneration
    solution: food
    duration: 1
    generated:
      reagents:
      - ReagentId: Water
        Quantity: 3

- type: entity
  id: IS14TestDirectRegeneration
  components:
  - type: Solution
    id: food
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 7
  - type: SolutionRegeneration
    duration: 1
    generated:
      reagents:
      - ReagentId: Water
        Quantity: 3
";

    [TestCase("IS14TestLegacyRegeneration")]
    [TestCase("IS14TestDirectRegeneration")]
    public async Task RegenerationSupportsBothSolutionLayouts(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid entity = default;
        Entity<SolutionComponent> target = default;
        await server.WaitAssertion(() =>
        {
            entity = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedSolutionContainerSystem>()
                .TryGetSolution(entity, "food", out var solution), Is.True);
            target = solution!.Value;
        });
        await PoolManager.WaitUntil(server, () => target.Comp.Solution.Volume >= FixedPoint2.New(10));
        await server.WaitAssertion(() =>
        {
            Assert.That(target.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(target.Comp.Solution.MaxVolume, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(target.Comp.Solution.ContainsPrototype(CompatibilityWaterReagentId), Is.True);
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MigrationRetainsDestinationAndContentsAfterDeferredRemoval()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid entity = default;
        await server.WaitPost(() => entity = server.EntMan.SpawnEntity("IS14TestLegacyEdibleSolution", map.GridCoords));
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<SolutionManagerComponent>(entity), Is.True,
                "The migration must retain the new solution manager.");
            Assert.That(server.EntMan.HasComponent<SolutionContainerManagerComponent>(entity), Is.False,
                "The obsolete source component must be removed after migration.");
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(entity, "food", out _, out var solution), Is.True);
            Assert.That(solution!.MaxVolume, Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(7)));
            Assert.That(solution.ContainsPrototype(CompatibilityWaterReagentId), Is.True);
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}

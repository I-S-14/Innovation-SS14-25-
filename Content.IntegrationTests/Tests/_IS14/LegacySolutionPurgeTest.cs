using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacySolutionPurgeTest
{
    private static readonly ProtoId<ReagentPrototype> CompatibilityWaterReagentId = "Water";
    private static readonly ProtoId<ReagentPrototype> CompatibilityDexalinPlusReagentId = "DexalinPlus";

    [Test]
    public async Task MoproachRegeneratesCleaningWaterAndPurgesContaminants()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid entity = default;
        Entity<SolutionComponent> target = default;
        await server.WaitAssertion(() =>
        {
            entity = server.EntMan.SpawnEntity("MobMoproach", map.GridCoords);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(entity, "absorbed", out var solution), Is.True);
            target = solution!.Value;
            Assert.That(target.Comp.Solution.MaxVolume, Is.EqualTo(FixedPoint2.New(50)));
            Assert.That(solutions.TryAddSolution(target, new Solution("DexalinPlus", FixedPoint2.New(5))), Is.True);
        });
        await PoolManager.WaitUntil(server, () => target.Comp.Solution.Volume >= FixedPoint2.New(10)
            && target.Comp.Solution.ContainsPrototype(CompatibilityWaterReagentId) && !target.Comp.Solution.ContainsPrototype(CompatibilityDexalinPlusReagentId));
        await server.WaitAssertion(() =>
        {
            Assert.That(target.Comp.Solution.MaxVolume, Is.EqualTo(FixedPoint2.New(50)));
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestLegacyPurge
  components:
  - type: SolutionContainerManager
    solutions:
      absorbed:
        maxVol: 20
        reagents:
        - ReagentId: Water
          Quantity: 7
        - ReagentId: DexalinPlus
          Quantity: 3
  - type: SolutionPurge
    solution: absorbed
    preserve: [Water]
    quantity: 3
- type: entity
  id: IS14TestDirectPurge
  components:
  - type: Solution
    id: absorbed
    solution:
      maxVol: 20
      reagents:
      - ReagentId: Water
        Quantity: 7
      - ReagentId: DexalinPlus
        Quantity: 3
  - type: SolutionPurge
    preserve: [Water]
    quantity: 3
";

    [TestCase("IS14TestLegacyPurge")]
    [TestCase("IS14TestDirectPurge")]
    public async Task PurgeRemovesContaminantsAndPreservesWater(string prototype)
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
                .TryGetSolution(entity, "absorbed", out var solution), Is.True);
            target = solution!.Value;
            Assert.That(target.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(10)));
        });
        await PoolManager.WaitUntil(server, () => !target.Comp.Solution.ContainsPrototype(CompatibilityDexalinPlusReagentId));
        await server.WaitAssertion(() =>
        {
            Assert.That(target.Comp.Solution.Volume, Is.EqualTo(FixedPoint2.New(7)));
            Assert.That(target.Comp.Solution.ContainsPrototype(CompatibilityWaterReagentId), Is.True);
            Assert.That(target.Comp.Solution.MaxVolume, Is.EqualTo(FixedPoint2.New(20)));
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}

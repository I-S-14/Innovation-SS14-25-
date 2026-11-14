using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class EggRehydrationCompatibilityTest
{
    private static readonly ProtoId<ReagentPrototype> CompatibilityEggCookedReagentId = "EggCooked";
    private static readonly ProtoId<ReagentPrototype> CompatibilityEggReagentId = "Egg";

    [TestCase("FoodEgg")]
    [TestCase("FoodEggChickenFertilized")]
    [TestCase("FoodEggDuckFertilized")]
    public async Task OnlyFiveUnitsOfMutagenTransformTheEgg(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid egg = default;
        EntityUid creature = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            egg = em.SpawnEntity(prototype, map.GridCoords);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(egg, "food", out var solution), Is.True);
            Assert.That(solution!.Value.Comp.Solution.MaxVolume, Is.EqualTo(FixedPoint2.New(11)));
            // Leave room for the negative control without changing the production egg's capacity.
            solution!.Value.Comp.Solution.MaxVolume = FixedPoint2.New(100);
            solutions.TryAddReagent(solution.Value, "Water", FixedPoint2.New(5), out _);
            Assert.That(em.HasComponent<RehydratableComponent>(egg), Is.True);
            solutions.TryAddReagent(solution.Value, "UnstableMutagen", FixedPoint2.New(4), out _);
            Assert.That(em.HasComponent<RehydratableComponent>(egg), Is.True);
            solutions.TryAddReagent(solution.Value, "UnstableMutagen", FixedPoint2.New(1), out _);
            Assert.That(em.HasComponent<RehydratableComponent>(egg), Is.False);
            var count = 0;
            var query = em.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var metadata))
            {
                if (metadata.EntityPrototype?.ID != "MobHomunculus")
                    continue;
                creature = uid;
                count++;
            }
            Assert.That(count, Is.EqualTo(1));
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(egg), Is.False);
            server.EntMan.DeleteEntity(creature);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BoiledEggKeepsItsCookedSolution()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var egg = server.EntMan.SpawnEntity("FoodEggBoiled", map.GridCoords);
            Assert.That(server.System<SharedSolutionContainerSystem>()
                .TryGetSolution(egg, "food", out _, out var solution), Is.True);
            Assert.That(solution!.MaxVolume, Is.EqualTo(FixedPoint2.New(6)));
            Assert.That(solution.Volume, Is.EqualTo(FixedPoint2.New(6)));
            Assert.That(solution.ContainsPrototype(CompatibilityEggCookedReagentId), Is.True);
            Assert.That(solution.ContainsPrototype(CompatibilityEggReagentId), Is.False);
            server.EntMan.DeleteEntity(egg);
        });
        await pair.CleanReturnAsync();
    }
}

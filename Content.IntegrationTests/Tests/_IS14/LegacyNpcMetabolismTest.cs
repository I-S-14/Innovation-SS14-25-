using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyNpcMetabolismTest
{
    [TestCase("MobDragon", "Omnizine")]
    [TestCase("MobBehonkerElectrical", "Omnizine")]
    [TestCase("MobBehonkerElectricalRedspace", "Toxin")]
    [TestCase("MobNukieBehonker", "Toxin")]
    public async Task RetainedCreaturesMetabolizeMedicineAndPoison(string prototype, string reagent)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid entity = default;
        Entity<SolutionComponent> target = default;
        await server.WaitAssertion(() =>
        {
            entity = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var metabolizer = server.EntMan.GetComponent<MetabolizerComponent>(entity);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(entity, metabolizer.SolutionName, out var solution), Is.True);
            target = solution!.Value;
            // Leave room for a controlled dose without replacing the creature's blood.
            solutions.SetCapacity(target, target.Comp.Solution.MaxVolume + FixedPoint2.New(5));
            Assert.That(solutions.TryAddSolution(target, new Solution(reagent, FixedPoint2.New(5))), Is.True);
        });
        await PoolManager.WaitUntil(server, () => target.Comp.Solution.GetTotalPrototypeQuantity(reagent) < FixedPoint2.New(5));
        await server.WaitAssertion(() => server.EntMan.DeleteEntity(entity));
        await pair.CleanReturnAsync();
    }
}

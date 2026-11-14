using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyBloodstreamInitializationTest
{
    [TestCase("MobHuman")]
    [TestCase("MobMoproach")]
    [TestCase("MobClownSpider")]
    public async Task FreshBodiesHaveBloodAndChemicalCapacity(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entity = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var bloodstream = server.EntMan.GetComponent<BloodstreamComponent>(entity);
            var solutions = server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(entity, bloodstream.BloodSolutionName, out _, out var blood), Is.True);
            Assert.That(blood!.Volume, Is.EqualTo(bloodstream.BloodReferenceSolution.Volume));
            Assert.That(blood.Volume.Float(), Is.GreaterThan(0));
            if (prototype == "MobClownSpider")
            {
                Assert.That(blood.GetTotalPrototypeQuantity("Laughter").Float(), Is.EqualTo(150));
                Assert.That(blood.GetTotalPrototypeQuantity("Blood").Float(), Is.Zero);
            }
            Assert.That(solutions.TryGetSolution(entity, bloodstream.BloodTemporarySolutionName, out _, out var chemicals), Is.True);
            Assert.That(chemicals!.MaxVolume.Float(), Is.GreaterThan(0));
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}

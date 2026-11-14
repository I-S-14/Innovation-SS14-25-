using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using System.Numerics;
using Content.Shared.Botany.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class PinkSlimeExtractMutationTest
{
    private static readonly ProtoId<ReagentPrototype> CompatibilityWaterReagentId = "Water";

    [TestCase(ReactionMethod.Touch)]
    [TestCase(ReactionMethod.Injection)]
    public async Task WaterReactionMutatesOnlyLivingNearbyPlants(ReactionMethod method)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var extract = em.SpawnEntity("PinkSlimeExtract", map.GridCoords);
            var near = em.SpawnEntity("AloePlants", map.GridCoords.Offset(new Vector2(2, 0)));
            var far = em.SpawnEntity("AloePlants", map.GridCoords.Offset(new Vector2(8, 0)));
            var dead = em.SpawnEntity("AloePlants", map.GridCoords.Offset(new Vector2(-2, 0)));
            var nearHolder = em.GetComponent<PlantHolderComponent>(near);
            var farHolder = em.GetComponent<PlantHolderComponent>(far);
            var deadHolder = em.GetComponent<PlantHolderComponent>(dead);
            nearHolder.MutationMod = 2f;
            deadHolder.Dead = true;
            var water = new Solution();
            water.AddReagent(CompatibilityWaterReagentId, 1);
            server.System<ReactiveSystem>().DoEntityReaction(extract, water, method);
            Assert.That(nearHolder.MutationLevels.TryGetValue("RandomPlantMutations", out var level), Is.True);
            Assert.That(level, Is.EqualTo(2f), "The retained default amount must respect the plant mutation modifier.");
            Assert.That(farHolder.MutationLevels, Is.Empty, "Plants beyond the retained five-tile radius must be unaffected.");
            Assert.That(deadHolder.MutationLevels, Is.Empty, "The plant effect must still reject dead plants.");
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}

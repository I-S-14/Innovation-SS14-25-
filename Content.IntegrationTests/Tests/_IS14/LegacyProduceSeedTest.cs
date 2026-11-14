using System.Collections.Generic;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyProduceSeedTest
{
    [TestCase("FoodCucumber", "IS14CucumbertSeedsPlant", "CucumbertSeeds")]
    [TestCase("FoodShashamber", "IS14ShashambertSeedsPlant", "ShashambertSeeds")]
    [TestCase("FoodMango", "IS14MangoSeedsPlant", "MangoSeeds")]
    [TestCase("FoodStrangeBerries", "IS14StrangeBerrySeedsPlant", "StrangeBerrySeeds")]
    [TestCase("FoodSpaceshroom", "IS14SpaceshroomSeedsPlant", "SpaceshroomSeeds")]
    [TestCase("FoodBluespaceTomato", "IS14BluespaceTomatoSeedsPlant", "BluespaceTomatoSeeds")]
    [TestCase("Nevcotta", "TowercapPlants", "TowercapSeeds")]
    [TestCase("AshwoodLog", "TowercapPlants", "TowercapSeeds")]
    public async Task RetainedProduceYieldsPlantSeedPackets(string produceId, string plantId, string packetId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var user = entities.SpawnEntity(null, map.GridCoords);
            var extractor = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<SeedExtractorComponent>(extractor);
            var fruit = entities.SpawnEntity(produceId, map.GridCoords);
            Assert.That(entities.GetComponent<ProduceComponent>(fruit).PlantProtoId?.Id, Is.EqualTo(plantId));
            var interaction = new InteractUsingEvent(user, fruit, extractor, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(extractor, interaction);
            Assert.That(interaction.Handled, Is.True);
            var packets = new List<EntityUid>();
            var query = entities.AllEntityQueryEnumerator<SeedComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out var seed, out var metadata))
            {
                if (metadata.EntityPrototype?.ID != packetId)
                    continue;
                Assert.That(seed.PlantProtoId.Id, Is.EqualTo(plantId));
                packets.Add(uid);
            }
            Assert.That(packets.Count, Is.InRange(1, 3));
            foreach (var packet in packets)
                entities.DeleteEntity(packet);
            entities.DeleteEntity(fruit);
            entities.DeleteEntity(extractor);
            entities.DeleteEntity(user);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RetainedBluespacePacketReferencesMigratedPlant()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var packet = entities.SpawnEntity("BluespaceTomatoSeeds", map.GridCoords);
            Assert.That(entities.GetComponent<SeedComponent>(packet).PlantProtoId.Id, Is.EqualTo("IS14BluespaceTomatoSeedsPlant"));
            entities.DeleteEntity(packet);
        });
        await pair.CleanReturnAsync();
    }
}

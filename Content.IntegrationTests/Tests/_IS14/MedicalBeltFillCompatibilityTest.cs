using System.Linq;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MedicalBeltFillCompatibilityTest
{
    [Test]
    public async Task ParamedicBeltContainsAllRetainedMedicalEquipment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var belt = em.SpawnEntity("ClothingBeltMedicalEMTFilled", map.GridCoords);
            var items = server.System<SharedContainerSystem>().GetContainer(belt, "storagebase").ContainedEntities.ToArray();
            Assert.That(items.Select(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID),
                Is.EquivalentTo(new[]
                {
                    "Brutepack", "Ointment", "Bloodpack", "Gauze", "HandheldCrewMonitor", "ParamedHypo",
                    "CartridgeSaline", "CartridgeBicaridine", "CartridgeDermaline", "CartridgeAtropine",
                }));
            foreach (var item in items)
                em.DeleteEntity(item);
            em.DeleteEntity(belt);
        });
        await pair.CleanReturnAsync();
    }
}

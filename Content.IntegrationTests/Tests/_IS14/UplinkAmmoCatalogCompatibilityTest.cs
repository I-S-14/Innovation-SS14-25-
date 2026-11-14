using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class UplinkAmmoCatalogCompatibilityTest
{
    [TestCase("", "GrenadeBlast", 12, true)]
    [TestCase("", "GrenadeFrag", 12, true)]
    [TestCase("", "MagazineShotgun", 5, true)]
    [TestCase("NukeOpsUplink", "GrenadeBlast", 12, true)]
    [TestCase("NukeOpsUplink", "GrenadeFrag", 12, true)]
    [TestCase("NukeOpsUplink", "MagazineShotgun", 5, true)]
    [TestCase("ContractorUplink", "GrenadeBlast", 12, false)]
    [TestCase("ContractorUplink", "GrenadeFrag", 12, false)]
    [TestCase("ContractorUplink", "MagazineShotgun", 5, true)]
    public async Task AmmunitionKeepsRetainedPriceAndStoreRestrictions(string tag, string product, int price, bool allowed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var human = em.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var mind = server.System<SharedMindSystem>().CreateMind(null);
            server.System<SharedMindSystem>().TransferTo(mind, human, createGhost: false);
            var storeUid = em.SpawnEntity("IS14RetainedUplinkStorePreset", MapCoordinates.Nullspace);
            if (tag.Length > 0)
                server.System<TagSystem>().AddTag(storeUid, tag);
            var store = em.GetComponent<StoreComponent>(storeUid);
            var offers = server.System<SharedStoreSystem>().GetAvailableListings(mind, storeUid, store)
                .Where(listing => listing.ProductEntity?.Id == product).ToArray();
            Assert.That(offers, Has.Length.EqualTo(allowed ? 1 : 0));
            if (allowed)
                Assert.That(offers[0].Cost["Telecrystal"], Is.EqualTo(FixedPoint2.New(price)));
            em.DeleteEntity(storeUid);
            em.DeleteEntity(human);
            em.DeleteEntity(mind);
        });
        await pair.CleanReturnAsync();
    }
}

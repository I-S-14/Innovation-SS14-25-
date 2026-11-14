using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class UplinkJobCatalogCompatibilityTest
{
    [TestCase("Botanist", "GatfruitSeeds", "uplinkGatfruitSeeds", 30)]
    [TestCase("Chaplain", "HolyHandGrenade", "uplinkHolyHandGrenade", 100)]
    [TestCase("Clown", "RevolverCapGunFake", "uplinkRevolverCapGunFake", 20)]
    [TestCase("Chef", "HotPotato", "uplinkHotPotato", 20)]
    [TestCase("Janitor", "WetFloorSignMineExplosive", "uplinkProximityMine", 16)]
    [TestCase("Lawyer", "WeaponSubMachineGunBriefcase", "uplinkBriefcaseGun", 25)]
    public async Task JobProductHasOneOfferAtRetainedPrice(string job, string product, string listingId, int price)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var human = em.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var mind = server.System<SharedMindSystem>().CreateMind(null);
            server.System<SharedMindSystem>().TransferTo(mind, human, createGhost: false);
            server.System<SharedRoleSystem>().MindAddJobRole(mind, silent: true, jobPrototype: job);
            var storeUid = em.SpawnEntity("IS14RetainedUplinkStorePreset", MapCoordinates.Nullspace);
            var store = em.GetComponent<StoreComponent>(storeUid);
            var offers = server.System<SharedStoreSystem>().GetAvailableListings(mind, storeUid, store)
                .Where(listing => listing.ProductEntity?.Id == product).ToArray();
            Assert.That(offers, Has.Length.EqualTo(1));
            Assert.That(offers[0].ID, Is.EqualTo(listingId));
            Assert.That(offers[0].Cost["Telecrystal"], Is.EqualTo(FixedPoint2.New(price)));
            em.DeleteEntity(storeUid);
            em.DeleteEntity(human);
            em.DeleteEntity(mind);
        });
        await pair.CleanReturnAsync();
    }
}

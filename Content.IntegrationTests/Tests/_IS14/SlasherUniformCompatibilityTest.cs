using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class SlasherUniformCompatibilityTest
{
    [TestCase("ClothingUniformSlasherMaintenance")]
    [TestCase("ClothingUniformSlasherCannibal")]
    [TestCase("ClothingUniformSlasherClown")]
    [TestCase("ClothingUniformSlasherClownAscended")]
    [TestCase("ClothingUniformSlasherHockey")]
    [TestCase("ClothingUniformSlasherBoogeyman")]
    public async Task CustomUniformCanBeHeldAndSelfEquippedButNotSelfRemoved(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, uniform = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            uniform = em.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, uniform), Is.True);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(human, uniform, "jumpsuit", silent: true), Is.True);
            Assert.That(inventory.TryUnequip(human, "jumpsuit", silent: true), Is.False,
                "The retained Slasher uniform must still prohibit removing it yourself.");
        });
        await pair.RunTicksSync(3);
        await server.WaitPost(() => server.EntMan.DeleteEntity(human));
        await pair.CleanReturnAsync();
    }
}

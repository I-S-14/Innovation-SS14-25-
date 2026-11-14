using Content.Server.Polymorph.Systems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedAdminMorphTest
{
    [Test]
    public async Task VulpSmiteKeepsThePlayersMindNameAndEquippedUniform()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, mind = default, uniform = default, vulp = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            server.System<MetaDataSystem>().SetEntityName(human, "Upstream morph test");
            mind = server.System<SharedMindSystem>().CreateMind(null).Owner;
            server.System<SharedMindSystem>().TransferTo(mind, human);
            uniform = em.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(human, uniform, "jumpsuit", silent: true), Is.True);
            var result = server.System<PolymorphSystem>().PolymorphEntity(human, "AdminVulpSmite");
            Assert.That(result, Is.Not.Null);
            vulp = result!.Value;
            Assert.That(em.GetComponent<MetaDataComponent>(vulp).EntityPrototype!.ID, Is.EqualTo("MobVulpkanin"));
            Assert.That(em.GetComponent<MetaDataComponent>(vulp).EntityName, Is.EqualTo("Upstream morph test"));
            Assert.That(em.GetComponent<MindComponent>(mind).CurrentEntity, Is.EqualTo(vulp));
            Assert.That(inventory.TryGetSlotEntity(vulp, "jumpsuit", out var transferred), Is.True);
            Assert.That(transferred, Is.EqualTo(uniform));
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<PolymorphSystem>().Revert(vulp), Is.EqualTo(human));
            var em = server.EntMan;
            Assert.That(em.GetComponent<MindComponent>(mind).CurrentEntity, Is.EqualTo(human));
            Assert.That(server.System<InventorySystem>().TryGetSlotEntity(human, "jumpsuit", out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(uniform));
            em.DeleteEntity(human);
            em.DeleteEntity(mind);
        });
        await pair.CleanReturnAsync();
    }
}

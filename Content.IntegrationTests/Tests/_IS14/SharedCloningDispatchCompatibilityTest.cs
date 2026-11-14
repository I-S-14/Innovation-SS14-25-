using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Cloning;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class SharedCloningDispatchCompatibilityTest : GameTest
{
    [Test]
    public async Task SharedApiCopiesAnItem()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var original = entities.SpawnEntity("Crowbar", map.GridCoords);
            var copy = Server.System<SharedCloningSystem>().CopyItem(original, map.GridCoords);
            Assert.That(copy, Is.Not.Null.And.Not.EqualTo(original));
            Assert.That(entities.GetComponent<MetaDataComponent>(copy!.Value).EntityPrototype?.ID, Is.EqualTo("Crowbar"));
        });
    }

    [Test]
    public async Task SharedApiCopiesEquipment()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var original = entities.SpawnEntity("MobHuman", map.GridCoords);
            var target = entities.SpawnEntity("MobHuman", map.GridCoords);
            var backpack = entities.SpawnEntity("ClothingBackpack", map.GridCoords);
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(original, backpack, "back", silent: true, force: true), Is.True);
            Server.System<SharedCloningSystem>().CopyEquipment(original, target, SlotFlags.BACK);
            Assert.That(inventory.TryGetSlotEntity(target, "back", out var copied), Is.True);
            Assert.That(copied, Is.Not.EqualTo(backpack));
            Assert.That(entities.GetComponent<MetaDataComponent>(copied!.Value).EntityPrototype?.ID, Is.EqualTo("ClothingBackpack"));
        });
    }

    [Test]
    public async Task SharedApiCreatesAClone()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var original = entities.SpawnEntity("MobHuman", map.GridCoords);
            var coordinates = Server.System<SharedTransformSystem>().GetMapCoordinates(original);
            var cloning = Server.System<SharedCloningSystem>();
            Assert.That(cloning.TryCloning(original, coordinates, "TraitsMental", out var clone), Is.True);
            Assert.That(clone, Is.Not.Null.And.Not.EqualTo(original));
            Assert.That(entities.GetComponent<MetaDataComponent>(clone!.Value).EntityPrototype?.ID, Is.EqualTo("MobHuman"));
        });
    }

    [Test]
    public async Task SharedApiCopiesStorageContents()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var original = entities.SpawnEntity("ClothingBackpack", map.GridCoords);
            var target = entities.SpawnEntity("ClothingBackpack", map.GridCoords);
            var item = entities.SpawnEntity("Crowbar", map.GridCoords);
            Assert.That(Server.System<SharedStorageSystem>().Insert(original, item, out _, playSound: false), Is.True);
            Server.System<SharedCloningSystem>().CopyStorage(original, target);
            var copied = entities.GetComponent<StorageComponent>(target).StoredItems.Keys.Single();
            Assert.That(copied, Is.Not.EqualTo(item));
            Assert.That(entities.GetComponent<MetaDataComponent>(copied).EntityPrototype?.ID, Is.EqualTo("Crowbar"));
        });
    }

    [Test]
    public async Task SharedApiCopiesImplants()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var original = entities.SpawnEntity("MobHuman", map.GridCoords);
            var target = entities.SpawnEntity("MobHuman", map.GridCoords);
            var implant = Server.System<SharedSubdermalImplantSystem>().AddImplant(original, "MindShieldImplant");
            Assert.That(implant, Is.Not.Null);
            Server.System<SharedCloningSystem>().CopyImplants(original, target);
            var copied = entities.GetComponent<ImplantedComponent>(target).ImplantContainer.ContainedEntities.Single();
            Assert.That(copied, Is.Not.EqualTo(implant));
            Assert.That(entities.GetComponent<MetaDataComponent>(copied).EntityPrototype?.ID, Is.EqualTo("MindShieldImplant"));
        });
    }
}

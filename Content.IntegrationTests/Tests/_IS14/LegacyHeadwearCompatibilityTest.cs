using Content.Shared.Clothing.Components;
using Content.Shared.Foldable;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyHeadwearCompatibilityTest
{
    [TestCase("ClothingHeadHelmetBasicAlt")]
    [TestCase("ClothingHeadHelmetSecurityMedic")]
    public async Task LegacyHelmetRetainsGoobHairToggleWithoutRequiringVisorArt(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, helmet = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            helmet = em.SpawnEntity(prototype, map.GridCoords);
            var foldable = em.GetComponent<FoldableComponent>(helmet);
            var hide = em.GetComponent<HideLayerClothingComponent>(helmet);
            var folding = server.System<FoldableSystem>();
            Assert.That(hide.Layers.ContainsKey(HumanoidVisualLayers.Hair), Is.True);
            Assert.That(folding.TryToggleFold(helmet, foldable, human), Is.True);
            Assert.That(hide.Layers.ContainsKey(HumanoidVisualLayers.Hair), Is.False);
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(human, helmet, "head", silent: true), Is.True);
            Assert.That(folding.TryToggleFold(helmet, foldable, human), Is.False,
                "The retained Goob hair toggle is only available while the helmet is not worn.");
            Assert.That(inventory.TryUnequip(human, "head", silent: true), Is.True);
            Assert.That(folding.TryToggleFold(helmet, foldable, human), Is.True);
            Assert.That(hide.Layers.ContainsKey(HumanoidVisualLayers.Hair), Is.True);
            Assert.That(inventory.TryEquip(human, helmet, "head", silent: true), Is.True);
        });
        await pair.RunTicksSync(3);
        await server.WaitPost(() => server.EntMan.DeleteEntity(human));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegacyClownMaskCanBeEquippedWithoutAddingAnUnsupportedWigToggle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            var mask = em.SpawnEntity("ClothingMaskSexyClown", map.GridCoords);
            Assert.That(em.HasComponent<FoldableComponent>(mask), Is.False);
            Assert.That(server.System<InventorySystem>().TryEquip(human, mask, "mask", silent: true), Is.True);
        });
        await pair.RunTicksSync(3);
        await server.WaitPost(() => server.EntMan.DeleteEntity(human));
        await pair.CleanReturnAsync();
    }
}

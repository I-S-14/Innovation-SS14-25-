using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ClothingRsiCompatibilityTest : GameTest
{
    [TestCase("off", "human")]
    [TestCase("on", "human")]
    [TestCase("off", "vox")]
    [TestCase("on", "vox")]
    [TestCase("off", "vulpkanin")]
    [TestCase("on", "vulpkanin")]
    public async Task LegacySecurityHelmetKeepsLightAndSpeciesStates(string prefix, string species)
    {
        await Client.WaitAssertion(() =>
        {
            var entities = Client.EntMan;
            var wearer = entities.Spawn("MobHuman");
            var helmet = entities.Spawn("ClothingHeadHelmetVoidsuitSecurity");
            Client.System<InventorySystem>().SetSpeciesId((wearer, entities.GetComponent<InventoryComponent>(wearer)), species);
            Client.System<ClothingSystem>().SetEquippedPrefix(helmet, prefix);
            var visuals = new GetEquipmentVisualsEvent(wearer, "head");
            entities.EventBus.RaiseLocalEvent(helmet, visuals);
            var expected = prefix + "-equipped-HELMET" + (species == "human" ? "" : "-" + species);
            Assert.That(visuals.Layers.Select(x => x.Item2.State), Is.EquivalentTo(new[] { expected }));
            entities.DeleteEntity(helmet);
            entities.DeleteEntity(wearer);
        });
    }

    [Test]
    public async Task AllExplicitClothingVisualsReferenceExistingStates()
    {
        await Client.WaitAssertion(() =>
        {
            var entities = Client.EntMan;
            var factory = Client.ResolveDependency<IComponentFactory>();
            var cache = Client.ResolveDependency<IResourceCache>();
            var wearer = entities.Spawn("MobHuman");
            var inventory = entities.GetComponent<InventoryComponent>(wearer);
            var missing = new Dictionary<string, HashSet<string>>();
            var checkedLayers = 0;
            foreach (var prototype in Client.ProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract || Pair.IsTestPrototype(prototype)
                    || !prototype.TryComp<ClothingComponent>(out var config, factory)
                    || config.ClothingVisuals.Count == 0 || !prototype.HasComp<SpriteComponent>(factory))
                    continue;

                var item = entities.Spawn(prototype.ID);
                var sprite = entities.GetComponent<SpriteComponent>(item);
                foreach (var visualKey in config.ClothingVisuals.Keys)
                {
                    var parts = visualKey.Split('-', 2);
                    Client.System<InventorySystem>().SetSpeciesId((wearer, inventory), parts.Length == 2 ? parts[1] : null);
                    var visuals = new GetEquipmentVisualsEvent(wearer, parts[0]);
                    entities.EventBus.RaiseLocalEvent(item, visuals);
                    foreach (var (_, layer) in visuals.Layers)
                    {
                        if (layer.State == null || layer.TexturePath != null)
                            continue;
                        var rsi = layer.RsiPath == null ? sprite.BaseRSI
                            : cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / layer.RsiPath).RSI;
                        checkedLayers++;
                        if (rsi != null && rsi.TryGetState(layer.State, out _))
                            continue;
                        var key = $"{rsi?.Path}: {layer.State}";
                        if (!missing.TryGetValue(key, out var users))
                            missing[key] = users = new HashSet<string>();
                        users.Add(prototype.ID);
                    }
                }
                entities.DeleteEntity(item);
            }
            entities.DeleteEntity(wearer);
            TestContext.Out.WriteLine("CLOTHING_RSI_AUDIT=" + JsonSerializer.Serialize(missing));
            Assert.That(checkedLayers, Is.GreaterThan(100));
            Assert.That(missing, Is.Empty, $"Missing clothing states: {missing.Count}. See CLOTHING_RSI_AUDIT.");
        });
    }
}

using System.Collections.Generic;
using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Paper;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class CargoPackagingTest
{
    private static readonly ProtoId<CargoProductPrototype> CompatibilityFunArtSuppliesCargoProductId = "FunArtSupplies";
    private static readonly ProtoId<CargoProductPrototype> CompatibilityMaterialPlasteelCargoProductId = "MaterialPlasteel";
    private readonly ProtoId<CargoProductPrototype> CompatibilityIS14TestLiteralCargoCargoProductId = "IS14TestLiteralCargo";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestBulkCrate
  parent: CrateGeneric
  components:
  - type: EntityStorage
    capacity: 10
  - type: StorageFill
    contents:
    - id: CrayonBox
      amount: 2
- type: cargoProduct
  id: IS14TestLiteralCargo
  product: CrayonBox
  name: Старое название товара
  description: Старое описание товара
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeliveryPreservesParcelSecurityAndInvoice(bool secure)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var before = Entities(em);
            var product = server.ResolveDependency<IPrototypeManager>().Index<CargoProductPrototype>(CompatibilityFunArtSuppliesCargoProductId);
            Assert.That(product.Container, Is.Not.Null);
            var order = new CargoOrderData(42, product.Product, product.Name, product.Cost, 1,
                "Заказчик", "Бар", "Для праздника", "Cargo", product.Cooldown, secure, product.Container);
            order.SetApproverData("Снабженец");
            Assert.That(server.System<CargoSystem>().FulfillOrder(order, "Cargo", map.GridCoords, "Paper",
                out _, out _), Is.True);
            var created = Entities(em).Except(before).ToArray();
            EntityUid Find(string id) => created.Single(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == id);
            var box = Find("CrayonBox");
            var parcel = Find("WrappedParcel");
            Assert.That(server.System<SharedContainerSystem>().TryGetContainer(parcel, "contents", out var contents), Is.True);
            Assert.That(contents!.ContainedEntities, Does.Contain(box));
            if (secure)
            {
                var crate = Find("CratePrivateCargoSecure");
                Assert.That(em.GetComponent<EntityStorageComponent>(crate).Contents.ContainedEntities, Does.Contain(parcel));
            }
            else
                Assert.That(created.Any(uid => em.HasComponent<EntityStorageComponent>(uid)), Is.False);
            var paper = em.GetComponent<PaperComponent>(Find("Paper"));
            Assert.That(paper.Content, Does.Contain("Бар").And.Contain("Для праздника").And.Contain("Снабженец"));
            DeleteCreated(em, created);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BulkOrdersReuseCrateWithoutDuplicatingAutofill()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var before = Entities(em);
            var cargo = server.System<CargoSystem>();
            var order = new CargoOrderData(43, "IS14TestBulkCrate", "Оптовый товар", 100, 2,
                "Заказчик", "Бар", "Оптовый заказ", "Cargo", 0);
            Assert.That(cargo.FulfillOrder(order, "Cargo", map.GridCoords, "Paper", out var crate, out var excess), Is.True);
            Assert.That(crate, Is.Not.Null);
            Assert.That(excess, Is.Null);
            Assert.That(em.GetComponent<EntityStorageComponent>(crate!.Value).Contents.Count, Is.EqualTo(2));
            Assert.That(cargo.FulfillOrder(order, "Cargo", map.GridCoords, "Paper", out var nextCrate, out excess,
                previousCrate: crate), Is.True);
            Assert.That(nextCrate, Is.EqualTo(crate));
            Assert.That(excess, Is.Null);
            Assert.That(em.GetComponent<EntityStorageComponent>(crate.Value).Contents.Count, Is.EqualTo(4));
            var created = Entities(em).Except(before).ToArray();
            Assert.That(created.Count(uid => em.HasComponent<PaperComponent>(uid)), Is.EqualTo(1));
            DeleteCreated(em, created);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CatalogLocalizesKeysAndRetainsLiteralLabels()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            Assert.That(prototypes.Index<CargoProductPrototype>(CompatibilityMaterialPlasteelCargoProductId).Name, Does.Not.Contain("cargoproduct-"));
            var literal = prototypes.Index<CargoProductPrototype>(CompatibilityIS14TestLiteralCargoCargoProductId);
            Assert.That(literal.Name, Is.EqualTo("Старое название товара"));
            Assert.That(literal.Description, Is.EqualTo("Старое описание товара"));
        });
        await pair.CleanReturnAsync();
    }

    private static HashSet<EntityUid> Entities(IEntityManager manager)
    {
        var result = new HashSet<EntityUid>();
        var query = manager.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out _))
            result.Add(uid);
        return result;
    }

    private static void DeleteCreated(IEntityManager manager, IEnumerable<EntityUid> entities)
    {
        foreach (var entity in entities)
            if (manager.EntityExists(entity))
                manager.DeleteEntity(entity);
    }
}

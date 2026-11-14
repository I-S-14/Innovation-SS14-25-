using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ItemActionGrantCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestFlowerNeckOnly
  parent: SprayFlowerPin
  components:
  - type: ItemActionGrant
    restrictedSlots: [NECK]
- type: entity
  id: TestFlowerNeckOrHead
  parent: SprayFlowerPin
  components:
  - type: ItemActionGrant
    restrictedSlots: [NECK, HEAD]
";

    [TestCase("SprayFlowerPin", null, false)]
    [TestCase("SprayFlowerPin", SlotFlags.POCKET, false)]
    [TestCase("SprayFlowerPin", SlotFlags.NECK, true)]
    [TestCase("TestFlowerNeckOnly", SlotFlags.NECK, true)]
    [TestCase("TestFlowerNeckOnly", SlotFlags.HEAD, false)]
    [TestCase("TestFlowerNeckOrHead", SlotFlags.NECK, true)]
    [TestCase("TestFlowerNeckOrHead", SlotFlags.HEAD, true)]
    [TestCase("TestFlowerNeckOrHead", null, false)]
    public async Task WornFlowerGrantsItsActionOnlyInAllowedSlots(string prototype, SlotFlags? slot, bool expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var user = em.SpawnEntity("MobHuman", map.GridCoords);
            var flower = em.SpawnEntity(prototype, map.GridCoords);
            var grant = em.GetComponent<ItemActionGrantComponent>(flower);
            Assert.That(grant.ActiveIfWorn, Is.True);
            var ev = new GetItemActionsEvent(server.System<ActionContainerSystem>(), user, flower, slot);
            em.EventBus.RaiseLocalEvent(flower, ev);
            Assert.That(ev.Actions.Count, Is.EqualTo(expected ? 1 : 0));
            foreach (var action in ev.Actions)
                Assert.That(em.GetComponent<MetaDataComponent>(action).EntityPrototype!.ID, Is.EqualTo("ActionShootWater"));
            em.DeleteEntity(flower);
            em.DeleteEntity(user);
        });
        await pair.CleanReturnAsync();
    }
}

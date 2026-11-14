using Content.Shared.Hands.EntitySystems;
using Content.Shared.Holosign;
using Content.Shared.Interaction;
using Content.Shared.PowerCell;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class HolographicProjectorCompatibilityTest
{
    [TestCase("HoloprojectorCrate", "CrateHolographic", false)]
    [TestCase("RadiationBlockingProjector", "HolosignRadiationBlocking", true)]
    [TestCase("HoloprojectorSecurityAdvanced", "HolosignSecurityAdvanced", true)]
    public async Task UsingTheProjectorKeepsTheProjectionAnchoringAndConsumesOneCharge(
        string prototype, string projectionId, bool anchored)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var human = em.SpawnEntity("MobHuman", map.GridCoords);
            var projector = em.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, projector), Is.True);
            var cost = em.GetComponent<HolosignProjectorComponent>(projector).ChargeUse;
            var cells = server.System<PowerCellSystem>();
            var before = cells.GetRemainingUses(projector, cost);
            Assert.That(before, Is.GreaterThan(0));
            // CreateTestMap provides a floor at GridCoords; anchoring requires that floor.
            var click = map.GridCoords;
            var interaction = new BeforeRangedInteractEvent(human, projector, null, click, true);
            em.EventBus.RaiseLocalEvent(projector, interaction);
            Assert.That(interaction.Handled, Is.True);
            Assert.That(cells.GetRemainingUses(projector, cost), Is.EqualTo(before - 1));
            EntityUid projection = default;
            var count = 0;
            var query = em.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var metadata))
            {
                if (metadata.EntityPrototype?.ID != projectionId)
                    continue;
                projection = uid;
                count++;
            }
            Assert.That(count, Is.EqualTo(1));
            Assert.That(em.GetComponent<TransformComponent>(projection).Anchored, Is.EqualTo(anchored));
            em.DeleteEntity(projection);
            em.DeleteEntity(projector);
            em.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}

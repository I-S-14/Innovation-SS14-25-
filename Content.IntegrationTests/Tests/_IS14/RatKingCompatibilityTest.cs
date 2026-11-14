using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.RatKing;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RatKingCompatibilityTest
{
    [TestCase("MobRatKing")]
    [TestCase("MobRatKingBuff")]
    public async Task KingRaisesAnOwnedServantPaysHungerAndCannotOverspend(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var king = em.SpawnEntity(prototype, map.GridCoords);
            var hunger = server.System<HungerSystem>();
            hunger.SetHunger(king, 350);
            Assert.That(hunger.GetHunger(king), Is.EqualTo(350));
            var army = new RatKingRaiseArmyActionEvent { Performer = king };
            em.EventBus.RaiseLocalEvent(king, army);
            Assert.That(army.Handled, Is.True);
            Assert.That(hunger.GetHunger(king), Is.EqualTo(325));
            var comp = em.GetComponent<RatKingComponent>(king);
            Assert.That(comp.Servants, Has.Count.EqualTo(1));
            var servant = comp.Servants.Single();
            Assert.That(em.GetComponent<MetaDataComponent>(servant).EntityPrototype!.ID, Is.EqualTo("MobRatServant"));
            Assert.That(em.GetComponent<RatKingServantComponent>(servant).King, Is.EqualTo(king));
            var stay = new RatKingOrderActionEvent { Performer = king, Type = RatKingOrderType.Stay };
            em.EventBus.RaiseLocalEvent(king, stay);
            Assert.That(stay.Handled, Is.True);
            Assert.That(comp.CurrentOrder, Is.EqualTo(RatKingOrderType.Stay));
            hunger.SetHunger(king, 24);
            var hungry = new RatKingRaiseArmyActionEvent { Performer = king };
            em.EventBus.RaiseLocalEvent(king, hungry);
            Assert.That(hungry.Handled, Is.False);
            Assert.That(comp.Servants, Has.Count.EqualTo(1));
            Assert.That(hunger.GetHunger(king), Is.EqualTo(24));
            em.DeleteEntity(servant);
            em.DeleteEntity(king);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DomainAddsRealAmmoniaAndConsumesHungerOnlyWhenAffordable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        EntityUid map = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            map = server.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(server.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath("Maps/Test/Breathing/3by3-20oxy-80nit.yml"), out var grid), Is.True);
            var king = em.SpawnEntity("MobRatKing", new EntityCoordinates(grid!.Value.Owner, new Vector2(0.5f, 0.5f)));
            var hunger = server.System<HungerSystem>();
            hunger.SetHunger(king, 350);
            var air = server.System<AtmosphereSystem>().GetTileMixture(king, excite: true);
            Assert.That(air, Is.Not.Null);
            var before = air!.GetMoles(Gas.Ammonia);
            var domain = new RatKingDomainActionEvent { Performer = king };
            em.EventBus.RaiseLocalEvent(king, domain);
            Assert.That(domain.Handled, Is.True);
            Assert.That(hunger.GetHunger(king), Is.EqualTo(300));
            Assert.That(air.GetMoles(Gas.Ammonia) - before, Is.EqualTo(200).Within(0.001));
            hunger.SetHunger(king, 49);
            var hungry = new RatKingDomainActionEvent { Performer = king };
            em.EventBus.RaiseLocalEvent(king, hungry);
            Assert.That(hungry.Handled, Is.False);
            Assert.That(hunger.GetHunger(king), Is.EqualTo(49));
            Assert.That(air.GetMoles(Gas.Ammonia) - before, Is.EqualTo(200).Within(0.001));
            em.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}

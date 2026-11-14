using System.Linq;
using Content.Goobstation.Common.Religion;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Bible;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class BibleUseCompatibilityTest
{
    [TestCase("Bible", true, false, 0, 15)]
    [TestCase("Bible", true, true, 0, 30)]
    // Goob fixed-point splits truncate per part: 10/11 * 11 = 9.90,
    // 20/11 * 11 = 19.91, 50/11 * 11 = 49.94. Failure's 25 Asphyxiation
    // is split over three vital parts: 15 physical + 24.99 = 39.99.
    [TestCase("Bible", false, false, 9.9, 0)]
    [TestCase("BibleNecronomicon", true, false, 0, 19.91)]
    [TestCase("BibleNecronomicon", true, true, 0, 39.99)]
    [TestCase("BibleNecronomicon", false, false, 49.94, 0)]
    public async Task BookUseRetainsHealingFailuresUntrainedDamageAndConfiguredLight(
        string prototype, bool trained, bool fail, double expectedUserDamage, double expectedTargetDamage)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default, target = default, book = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            user = em.SpawnEntity("MobHuman", map.GridCoords);
            target = em.SpawnEntity("MobHuman", map.GridCoords);
            if (trained)
                em.AddComponent<BibleUserComponent>(user);
            book = em.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, book), Is.True);
            // Select each real handler branch deterministically; damage and effects
            // still come from the production prototype and normal interaction event.
            em.GetComponent<BibleComponent>(book).FailChance = fail ? 1 : 0;
            var damage = server.System<DamageableSystem>();
            if (trained && !fail && prototype == "Bible")
                damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } }, true);
            var use = new AfterInteractEvent(user, book, target, map.GridCoords, true);
            em.EventBus.RaiseLocalEvent(book, use);
            Assert.That(damage.GetTotalDamage(user), Is.EqualTo(FixedPoint2.New(expectedUserDamage)));
            Assert.That(damage.GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(expectedTargetDamage)));
            var effects = em.EntityQuery<MetaDataComponent>().Where(c =>
                c.EntityPrototype?.ID == "EvilLightEffect").ToArray();
            var expectedEffect = prototype == "BibleNecronomicon" && trained && !fail;
            Assert.That(effects.Length, Is.EqualTo(expectedEffect ? 1 : 0));
            // A repeated same-frame interaction must respect the retained cooldown.
            em.EventBus.RaiseLocalEvent(book, new AfterInteractEvent(user, book, target, map.GridCoords, true));
            Assert.That(damage.GetTotalDamage(user), Is.EqualTo(FixedPoint2.New(expectedUserDamage)));
            Assert.That(damage.GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(expectedTargetDamage)));
            foreach (var effect in effects)
                em.DeleteEntity(effect.Owner);
            em.DeleteEntity(book);
            em.DeleteEntity(target);
            em.DeleteEntity(user);
        });
        await pair.CleanReturnAsync();
    }
}

#nullable enable annotations
using System.Numerics;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Lock;
using Content.Shared.Mind;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedPolymorphProjectileTest
{
    [TestCase("WeaponWandPolymorphCarp", "ProjectilePolyboltCarp", "MobHuman", "MobCarpMagic")]
    [TestCase("WeaponWandPolymorphMonkey", "ProjectilePolyboltMonkey", "MobHuman", "MobMonkey")]
    [TestCase("WeaponWandCluwne", "ProjectilePolyboltCluwne", "MobHuman", "MobCluwne")]
    [TestCase("WeaponWandPolymorphBread", "ProjectilePolyboltBread", "MobHuman", "FoodBreadPlain")]
    [TestCase("WeaponWandPolymorphDoor", "ProjectilePolyboltDoor", "WallSolid", null)]
    public async Task PhysicalHitTransformsTheTargetAndCanBeReverted(string wandId, string boltId, string targetId, string? resultId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid target = default, wand = default, bolt = default;
        EntityUid? result = null;
        EntityUid? mind = null;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            target = em.SpawnEntity(targetId, map.GridCoords.Offset(new Vector2(3, 0)));
            if (targetId == "MobHuman")
            {
                mind = server.System<SharedMindSystem>().CreateMind(null).Owner;
                server.System<SharedMindSystem>().TransferTo(mind.Value, target);
            }
            wand = em.SpawnEntity(wandId, map.GridCoords);
            bolt = em.SpawnEntity(boltId, map.GridCoords);
            server.System<SharedGunSystem>().Shoot((wand, em.GetComponent<GunComponent>(wand)), bolt,
                map.GridCoords, map.GridCoords.Offset(new Vector2(10, 0)), out var impulse);
            Assert.That(impulse, Is.True);
        });
        await PoolManager.WaitUntil(server, () =>
        {
            var query = server.EntMan.EntityQueryEnumerator<PolymorphedEntityComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Parent != target)
                    continue;
                result = uid;
                return true;
            }
            return false;
        });
        await server.WaitAssertion(() =>
        {
            var actual = server.EntMan.GetComponent<MetaDataComponent>(result!.Value).EntityPrototype!.ID;
            if (resultId != null)
                Assert.That(actual, Is.EqualTo(resultId));
            else
                Assert.That(actual, Is.AnyOf("MetalDoor", "WoodDoor", "PlasmaDoor", "GoldDoor", "SilverDoor", "PaperDoor", "BananiumDoor"));
            if (mind != null)
                Assert.That(server.EntMan.GetComponent<MindComponent>(mind.Value).CurrentEntity, Is.EqualTo(result.Value));
            Assert.That(server.System<PolymorphSystem>().Revert(result.Value), Is.EqualTo(target));
            Assert.That(server.EntMan.EntityExists(target), Is.True);
            if (mind != null)
                Assert.That(server.EntMan.GetComponent<MindComponent>(mind.Value).CurrentEntity, Is.EqualTo(target));
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            if (server.EntMan.EntityExists(bolt))
                server.EntMan.DeleteEntity(bolt);
            server.EntMan.DeleteEntity(target);
            if (mind != null)
                server.EntMan.DeleteEntity(mind.Value);
            server.EntMan.DeleteEntity(wand);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DoorBoltDoesNotTransformAHuman()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, wand = default, bolt = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            wand = em.SpawnEntity("WeaponWandPolymorphDoor", map.GridCoords);
            bolt = em.SpawnEntity("ProjectilePolyboltDoor", map.GridCoords);
            server.System<SharedGunSystem>().Shoot((wand, em.GetComponent<GunComponent>(wand)), bolt,
                map.GridCoords, map.GridCoords.Offset(new Vector2(10, 0)), out var impulse);
            Assert.That(impulse, Is.True);
        });
        await PoolManager.WaitUntil(server, () => !server.EntMan.EntityExists(bolt));
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(human), Is.True);
            var query = em.EntityQueryEnumerator<PolymorphedEntityComponent>();
            while (query.MoveNext(out _, out var morph))
                Assert.That(morph.Parent, Is.Not.EqualTo(human));
            em.DeleteEntity(human);
            em.DeleteEntity(wand);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FiredCursedLockerCapturesAndLocksItsTarget()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid target = default, wand = default, locker = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            target = em.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            wand = em.SpawnEntity("WeaponWandLocker", map.GridCoords);
            locker = em.SpawnEntity("ProjectileLocker", map.GridCoords);
            server.System<SharedGunSystem>().Shoot((wand, em.GetComponent<GunComponent>(wand)), locker,
                map.GridCoords, map.GridCoords.Offset(new Vector2(10, 0)), out var impulse);
            Assert.That(impulse, Is.True);
        });
        await PoolManager.WaitUntil(server, () => server.EntMan.HasComponent<InsideEntityStorageComponent>(target));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<InsideEntityStorageComponent>(target).Storage, Is.EqualTo(locker));
            Assert.That(server.System<LockSystem>().IsLocked(locker), Is.True);
            server.EntMan.DeleteEntity(target);
            server.EntMan.DeleteEntity(locker);
            server.EntMan.DeleteEntity(wand);
        });
        await pair.CleanReturnAsync();
    }
}

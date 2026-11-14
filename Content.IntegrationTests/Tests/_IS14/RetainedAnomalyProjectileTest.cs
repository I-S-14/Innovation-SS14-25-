using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedAnomalyProjectileTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: damageContainer
  id: IS14TestAnomalyProjectileDamage
  supportedGroups: [Brute, Burn]
  supportedTypes: [Structural]

- type: entity
  id: IS14TestAnomalyProjectileTarget
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      target:
        shape: !type:PhysShapeAabb
          bounds: '-0.4,-0.4,0.4,0.4'
        hard: true
        layer: [Impassable]
  - type: Damageable
    damageContainer: IS14TestAnomalyProjectileDamage
";

    [TestCase("ProjectileIcicle", 90)]
    [TestCase("ProjectileAnomalyFireball", 10)]
    [TestCase("ProjectileFireball", 10)]
    public async Task RetainedProjectileCanBeFiredAndDamagesAnObstacle(string prototype, int minimumDamage)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid gun = default;
        EntityUid projectile = default;
        EntityUid target = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            target = em.SpawnEntity("IS14TestAnomalyProjectileTarget", map.GridCoords.Offset(new Vector2(3, 0)));
            gun = em.SpawnEntity("WeaponShotgunKammerer", map.GridCoords);
            projectile = em.SpawnEntity(prototype, map.GridCoords);
            server.System<SharedGunSystem>().Shoot((gun, em.GetComponent<GunComponent>(gun)), projectile,
                map.GridCoords, map.GridCoords.Offset(new Vector2(10, 0)), out var impulse);
            Assert.That(impulse, Is.True);
        });
        await PoolManager.WaitUntil(server, () => server.System<DamageableSystem>().GetTotalDamage(target) >= minimumDamage);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            if (prototype == "ProjectileIcicle")
            {
                var damage = em.GetComponent<DamageableComponent>(target).Damage.DamageDict;
                Assert.That(damage["Piercing"], Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(damage["Cold"], Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(damage["Structural"], Is.EqualTo(FixedPoint2.New(50)));
            }
            if (em.EntityExists(projectile))
                em.DeleteEntity(projectile);
            em.DeleteEntity(target);
            em.DeleteEntity(gun);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DragonBreathKeepsBurningBeforeItsLifetimeExpires()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid breath = default;
        EntityUid target = default;
        await server.WaitAssertion(() =>
        {
            // Keep the probe within the blast radius without colliding with the
            // projectile: contact is supposed to end a dragon-breath projectile.
            target = server.EntMan.SpawnEntity("IS14TestAnomalyProjectileTarget", map.GridCoords.Offset(new Vector2(0, 1)));
            breath = server.EntMan.SpawnEntity("ProjectileDragonsBreath", map.GridCoords);
        });
        await PoolManager.WaitUntil(server, () => server.System<DamageableSystem>().GetTotalDamage(target) > 0);
        FixedPoint2 firstDamage = default;
        await server.WaitAssertion(() =>
        {
            firstDamage = server.System<DamageableSystem>().GetTotalDamage(target);
            Assert.That(server.EntMan.EntityExists(breath), Is.True);
        });
        await PoolManager.WaitUntil(server, () => server.System<DamageableSystem>().GetTotalDamage(target) > firstDamage);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(breath), Is.True));
        await PoolManager.WaitUntil(server, () => !server.EntMan.EntityExists(breath));
        await server.WaitAssertion(() => server.EntMan.DeleteEntity(target));
        await pair.CleanReturnAsync();
    }
}

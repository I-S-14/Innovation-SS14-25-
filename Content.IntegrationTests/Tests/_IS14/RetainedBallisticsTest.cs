using System.Collections.Generic;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._CorvaxGoob.Damage.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedBallisticsTest
{
    [TestCase("ShellShotgun", 6, "Piercing:10,Structural:15")]
    [TestCase("ShellShotgunIncendiary", 4, "Piercing:3,Heat:3")]
    [TestCase("ShellShotgunPractice", 6, "Blunt:1")]
    [TestCase("ShellShotgunImprovised", 10, "Piercing:3,Slash:3")]
    [TestCase("ShellShotgunUranium", 5, "Radiation:5,Piercing:5")]
    [TestCase("ShellShotgunSlug", 1, "Piercing:40,Structural:15")]
    [TestCase("ShellShotgunBeanbag", 1, "Blunt:10")]
    [TestCase("ShellShotgunFlare", 1, "Heat:5")]
    [TestCase("ShellTranquilizer", 1, "Blunt:1")]
    [TestCase("CartridgeAntiMateriel", 1, "Piercing:75")]
    public async Task FiringCartridgesCreatesTheirRetainedProjectiles(string cartridgeId, int count, string damages)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var gun = em.SpawnEntity("WeaponShotgunKammerer", map.GridCoords);
            var cartridge = em.SpawnEntity(cartridgeId, map.GridCoords);
            var system = server.System<SharedGunSystem>();
            // Exercise cartridge firing and spread creation directly; chambering is a separate concern.
            system.Shoot((gun, em.GetComponent<GunComponent>(gun)), cartridge,
                map.GridCoords, map.GridCoords.Offset(new System.Numerics.Vector2(10, 0)), out var impulse);
            Assert.That(impulse, Is.True);
            Assert.That(em.GetComponent<CartridgeAmmoComponent>(cartridge).Spent, Is.True);

            var projectiles = new List<EntityUid>();
            var query = em.EntityQueryEnumerator<ProjectileComponent>();
            while (query.MoveNext(out var uid, out var projectile))
            {
                if (projectile.Weapon != gun)
                    continue;
                projectiles.Add(uid);
                Assert.That(em.GetComponent<PhysicsComponent>(uid).LinearVelocity.Length(), Is.GreaterThan(0));
                foreach (var expected in damages.Split(','))
                {
                    var parts = expected.Split(':');
                    Assert.That(projectile.Damage.DamageDict[parts[0]], Is.EqualTo(FixedPoint2.New(int.Parse(parts[1]))));
                }
                if (cartridgeId == "ShellShotgun")
                    Assert.That(projectile.Damage.WoundSeverityMultipliers["Piercing"], Is.EqualTo(FixedPoint2.New(1.5)));
                if (cartridgeId == "ShellShotgunBeanbag")
                    Assert.That(em.GetComponent<StaminaDamageModifierOnCollideComponent>(uid).StaminaCoefficient, Is.EqualTo(5));
                if (cartridgeId == "CartridgeAntiMateriel")
                {
                    Assert.That(projectile.Damage.ArmorPenetration, Is.EqualTo(0.5f));
                    Assert.That(projectile.PenetrationThreshold, Is.EqualTo(FixedPoint2.New(100)));
                    Assert.That(em.GetComponent<StaminaDamageOnCollideComponent>(uid).Damage, Is.EqualTo(115));
                }
            }
            Assert.That(projectiles, Has.Count.EqualTo(count));

            if (cartridgeId == "ShellTranquilizer")
            {
                var solutions = server.System<SharedSolutionContainerSystem>();
                Assert.That(solutions.TryGetSolution(cartridge, "ammo", out _, out var shellSolution), Is.True);
                Assert.That(shellSolution!.Volume, Is.EqualTo(FixedPoint2.Zero));
                var pellet = projectiles[0];
                Assert.That(solutions.TryGetSolution(pellet, "ammo", out _, out var pelletSolution), Is.True);
                Assert.That(pelletSolution!.GetTotalPrototypeQuantity("Haloperidol"), Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(pelletSolution.GetTotalPrototypeQuantity("ChloralHydrate"), Is.EqualTo(FixedPoint2.Zero));
                var patient = em.SpawnEntity("MobHuman", map.GridCoords);
                var bloodstream = em.GetComponent<BloodstreamComponent>(patient);
                Assert.That(solutions.TryGetSolution(patient, bloodstream.BloodSolutionName,
                    out _, out var chemicals), Is.True);
                var before = chemicals!.GetTotalPrototypeQuantity("Haloperidol");
                var hit = new ProjectileHitEvent(em.GetComponent<ProjectileComponent>(pellet).Damage, patient);
                em.EventBus.RaiseLocalEvent(pellet, ref hit);
                Assert.That(chemicals.GetTotalPrototypeQuantity("Haloperidol") - before, Is.EqualTo(FixedPoint2.New(5)));
                Assert.That(pelletSolution.Volume, Is.EqualTo(FixedPoint2.Zero));
                em.DeleteEntity(patient);
            }

            foreach (var projectile in projectiles)
                em.DeleteEntity(projectile);
            em.DeleteEntity(cartridge);
            em.DeleteEntity(gun);
        });
        await pair.CleanReturnAsync();
    }
}

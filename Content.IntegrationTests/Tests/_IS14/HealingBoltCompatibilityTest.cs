#nullable enable annotations
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Tag;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared.Whitelist;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class HealingBoltCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestSoulTappedHealingTarget
  parent: MobHuman
  components:
  - type: Tag
    tags: [SoulTapped]
";

    [TestCase(null)]
    [TestCase("Undead")]
    [TestCase("SoulTapped")]
    public async Task FiredBoltHealsLivingBurnsUndeadAndRespectsSoulTap(string? tag)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid target = default, gun = default, bolt = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            // Keep the firing lane clear and give the target normal ambient air.
            // The one-tile default map otherwise allows vacuum damage between ticks.
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false,
                new GasMixture(moles, Atmospherics.T20C));
            for (var i = 1; i <= 4; i++)
                server.System<SharedMapSystem>().SetTile(map.Grid.Owner, map.Grid.Comp,
                    map.GridCoords.Offset(new Vector2(i, 0)), map.Tile.Tile);
            var gunCoords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var targetCoords = gunCoords.Offset(new Vector2(3, 0));
            var prototype = tag switch
            {
                "Undead" => "MobSkeletonPerson",
                "SoulTapped" => "TestSoulTappedHealingTarget",
                _ => "MobHuman",
            };
            target = em.SpawnEntity(prototype, targetCoords);
            server.System<DamageableSystem>().TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } }, true);
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(20)));
            if (tag != null)
                Assert.That(server.System<TagSystem>().HasTag(target, tag), Is.True);
            gun = em.SpawnEntity("WeaponStaffHealing", gunCoords);
            bolt = em.SpawnEntity("ProjectileHealingBolt", gunCoords);
            var effect = em.GetComponent<RejuvenateOnProjectileHitComponent>(bolt);
            Assert.That(server.System<EntityWhitelistSystem>().IsValid(effect.UndeadList, target), Is.EqualTo(tag == "Undead"));
            Assert.That(effect.ReverseEffects, Is.False);
            server.System<SharedGunSystem>().Shoot((gun, em.GetComponent<GunComponent>(gun)), bolt,
                gunCoords, targetCoords, out var impulse);
            Assert.That(impulse, Is.True);
        });
        await PoolManager.WaitUntil(server, () => !server.EntMan.EntityExists(bolt));
        await server.WaitAssertion(() =>
        {
            if (tag == "Undead")
            {
                // Skeletons gib at 150 damage. The retained 600-Heat hit destroys
                // the body, so querying its damage afterward would return zero.
                Assert.That(server.EntMan.EntityExists(target), Is.False);
            }
            else
            {
                Assert.That(server.EntMan.EntityExists(target), Is.True);
                Assert.That(server.System<DamageableSystem>().GetTotalDamage(target),
                    Is.EqualTo(FixedPoint2.New(tag == "SoulTapped" ? 20 : 0)));
                server.EntMan.DeleteEntity(target);
            }
            server.EntMan.DeleteEntity(gun);
        });
        await pair.CleanReturnAsync();
    }
}

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Radiation.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedRadiationProtectionTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestRadiationProtectionTarget
  components:
  - type: Damageable
    damageContainer: Biological
";

    [Test]
    public async Task ProtectionReducesRadiationAndIsRemovedCleanly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var target = em.SpawnEntity("IS14TestRadiationProtectionTarget", map.GridCoords);
            var damage = server.System<DamageableSystem>();
            var dose = new DamageSpecifier { DamageDict = { ["Radiation"] = 10 } };
            em.AddComponent<RadiationProtectionComponent>(target);
            Assert.That(em.HasComponent<DamageProtectionBuffComponent>(target), Is.True);
            damage.TryChangeDamage(target, dose);
            Assert.That(damage.GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(1)));
            em.RemoveComponent<RadiationProtectionComponent>(target);
            Assert.That(em.HasComponent<DamageProtectionBuffComponent>(target), Is.False);
            damage.TryChangeDamage(target, dose);
            Assert.That(damage.GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(11)));
            em.DeleteEntity(target);
        });
        await pair.CleanReturnAsync();
    }
}

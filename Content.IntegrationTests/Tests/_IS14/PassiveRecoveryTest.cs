using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class PassiveRecoveryTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestPassiveRecovery
  components:
  - type: Damageable
    damageContainer: Biological
  - type: PassiveDamage
    interval: 2
    intervalHaltOnDamageTaken: 5
    damage:
      types:
        Blunt: -1
";

    [Test]
    public async Task AllDueEntitiesRecoverAndDamagePostponesRecovery()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var damageSystem = server.System<DamageableSystem>();
            var timing = server.ResolveDependency<IGameTiming>();
            var entities = new EntityUid[2];
            for (var i = 0; i < entities.Length; i++)
            {
                var uid = entities[i] = server.EntMan.SpawnEntity("IS14TestPassiveRecovery", map.GridCoords);
                var passive = server.EntMan.GetComponent<PassiveDamageComponent>(uid);
                Assert.That(passive.NextDamage, Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(2)));
                damageSystem.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }, true);
                Assert.That(passive.NextDamage, Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(5)));
                passive.NextDamage = timing.CurTime;
            }
            server.System<PassiveDamageSystem>().Update(0);
            foreach (var uid in entities)
            {
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(uid).TotalDamage,
                    Is.EqualTo(FixedPoint2.New(9)), "Every due entity must recover in this update.");
                Assert.That(server.EntMan.GetComponent<PassiveDamageComponent>(uid).NextDamage,
                    Is.EqualTo(timing.CurTime + TimeSpan.FromSeconds(2)), "Healing must not trigger the positive-damage interruption.");
                server.EntMan.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }
}

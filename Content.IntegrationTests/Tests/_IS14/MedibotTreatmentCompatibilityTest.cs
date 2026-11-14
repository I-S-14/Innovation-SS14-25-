using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Silicons.Bots;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MedibotTreatmentCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestMedibotPatient
  components:
  - type: Damageable
    damageContainer: Biological
  - type: MobState
  - type: Solution
    id: chemicals
    solution:
      maxVol: 1000
  - type: InjectableSolution
    solution: chemicals
";

    [TestCase(0, false, false)]
    [TestCase(1, false, true)]
    [TestCase(99, false, true)]
    [TestCase(100, false, false)]
    [TestCase(120, false, false)]
    [TestCase(120, true, true)]
    [TestCase(0, true, false)]
    public async Task AutomaticTreatmentRespectsBoundsAndManualOverride(int damage, bool manual, bool expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var bot = em.SpawnEntity("MobMedibot", map.GridCoords);
            var patient = em.SpawnEntity("IS14TestMedibotPatient", map.GridCoords);
            var damages = server.System<DamageableSystem>();
            if (damage > 0)
                damages.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Blunt"] = damage } }, true);
            Assert.That(damages.GetTotalDamage(patient), Is.EqualTo(FixedPoint2.New(damage)));
            Assert.That(server.System<MedibotSystem>().CheckInjectable(bot, patient, manual), Is.EqualTo(expected));
            em.DeleteEntity(bot);
            em.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HumanReceivesTheRetainedDoseAndCannotBeImmediatelyReinjected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var bot = em.SpawnEntity("MobMedibot", map.GridCoords);
            var patient = em.SpawnEntity("MobHuman", map.GridCoords);
            var damages = server.System<DamageableSystem>();
            damages.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Blunt"] = 5 } }, true);
            var system = server.System<MedibotSystem>();
            Assert.That(system.CheckInjectable(bot, patient), Is.True);
            Assert.That(server.System<SharedSolutionContainerSystem>()
                .TryGetInjectableSolution(patient, out var injectable, out _), Is.True);
            var before = injectable!.Value.Comp.Solution.GetTotalPrototypeQuantity("Tricordrazine");
            Assert.That(system.TryInject(bot, patient), Is.True);
            Assert.That(injectable.Value.Comp.Solution.GetTotalPrototypeQuantity("Tricordrazine") - before,
                Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(em.HasComponent<NPCRecentlyInjectedComponent>(patient), Is.True);
            Assert.That(system.CheckInjectable(bot, patient), Is.False);
            Assert.That(system.CheckInjectable(bot, patient, manual: true), Is.False);
            Assert.That(system.TryInject(bot, patient), Is.False);
            Assert.That(injectable.Value.Comp.Solution.GetTotalPrototypeQuantity("Tricordrazine") - before,
                Is.EqualTo(FixedPoint2.New(30)));
            em.DeleteEntity(bot);
            em.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }
}

using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Disease.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Events;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DiseasePainNumbnessCompatibilityTest
{
    [Test]
    public async Task SymptomEffectsApplyAndRemovePainNumbnessWithoutHealingThePatient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, symptom = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            symptom = em.SpawnEntity("DiseaseBehaviorPainNumbness", map.GridCoords);
            server.System<DamageableSystem>().TryChangeDamage(human,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 30 } }, true);
            var initial = new BeforeAlertSeverityCheckEvent("HumanHealth", 1);
            em.EventBus.RaiseLocalEvent(human, initial);
            Assert.That(initial.CancelUpdate, Is.False);
            var effects = em.GetComponent<DiseaseReagentEffectComponent>(symptom).Effects;
            Assert.That(effects, Is.Not.Empty);
            foreach (var effect in effects)
                Assert.That(server.System<SharedEntityEffectsSystem>().TryApplyEffect(human, effect), Is.True);
            Assert.That(server.System<StatusEffectsSystem>().TryGetStatusEffect(human,
                "PainNumbnessTraitStatusEffect", out var active), Is.True);
            Assert.That(em.HasComponent<PainNumbnessStatusEffectComponent>(active!.Value), Is.True);
            var health = new BeforeAlertSeverityCheckEvent("HumanHealth", 1);
            em.EventBus.RaiseLocalEvent(human, health);
            Assert.That(health.CancelUpdate, Is.True);
            var speech = new BeforeForceSayEvent("ForceSayDataset");
            em.EventBus.RaiseLocalEvent(human, speech);
            Assert.That(speech.Prefix.Id, Is.EqualTo("ForceSayNumbDataset"));
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(human), Is.EqualTo(FixedPoint2.New(30)));
            Assert.That(server.System<StatusEffectsSystem>().TryRemoveStatusEffect(human,
                "PainNumbnessTraitStatusEffect"), Is.True);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(server.System<StatusEffectsSystem>().TryGetStatusEffect(human,
                "PainNumbnessTraitStatusEffect", out _), Is.False);
            var health = new BeforeAlertSeverityCheckEvent("HumanHealth", 1);
            em.EventBus.RaiseLocalEvent(human, health);
            Assert.That(health.CancelUpdate, Is.False);
            var speech = new BeforeForceSayEvent("ForceSayDataset");
            em.EventBus.RaiseLocalEvent(human, speech);
            Assert.That(speech.Prefix.Id, Is.EqualTo("ForceSayDataset"));
            em.DeleteEntity(symptom);
            em.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}

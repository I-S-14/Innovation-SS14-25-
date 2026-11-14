using Content.Goobstation.Shared.Wraith.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class WraithHauntFlashCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestHauntFlash
  parent: StatusEffectFlashed
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task HauntUsesTheConfiguredFlashEntityAndItExpires(bool customEffect)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid near = default, far = default;
        var effect = customEffect ? "IS14TestHauntFlash" : SharedFlashSystem.FlashedKey.Id;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var wraith = em.SpawnEntity("MobWraith", map.GridCoords);
            near = em.SpawnEntity("MobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            far = em.SpawnEntity("MobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(10, 0)));
            var haunt = em.GetComponent<HauntComponent>(wraith);
            Assert.That(haunt.HauntFlashDuration, Is.EqualTo(TimeSpan.FromSeconds(2)));
            if (customEffect)
                haunt.FlashedId = effect;
            var action = haunt.ActionEnt!.Value;
            server.System<SharedActionsSystem>().PerformAction((wraith, em.GetComponent<ActionsComponent>(wraith)),
                (action, em.GetComponent<ActionComponent>(action)));
            Assert.That(haunt.Active, Is.True);
            var effects = server.System<StatusEffectsSystem>();
            Assert.That(effects.TryGetStatusEffect(near, effect, out var status), Is.True);
            Assert.That(em.HasComponent<FlashedStatusEffectComponent>(status!.Value), Is.True);
            Assert.That(effects.HasStatusEffect(far, effect), Is.False);
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<StatusEffectsSystem>().HasStatusEffect(near, effect), Is.False);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}

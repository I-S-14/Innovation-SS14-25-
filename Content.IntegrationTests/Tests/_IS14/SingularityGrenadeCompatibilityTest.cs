using System.Numerics;
using Content.Server.Singularity.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Singularity.Components;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class SingularityGrenadeCompatibilityTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task FuseCreatesATemporaryGravityWell(bool damageStarted)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid grenade = default, item = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            grenade = em.SpawnEntity("SingularityGrenade", map.GridCoords);
            if (damageStarted)
                server.System<DamageableSystem>().TryChangeDamage(grenade,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }, true);
            else
            {
                var human = em.SpawnEntity("MobHuman", map.GridCoords);
                Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, grenade), Is.True);
                Assert.That(server.System<SharedInteractionSystem>().UseInHandInteraction(human, grenade), Is.True);
                // Place the primed grenade back on the floor for its real anchoring effect.
                Assert.That(server.System<SharedHandsSystem>().TryDrop(human, grenade, map.GridCoords), Is.True);
            }
            Assert.That(em.HasComponent<ActiveTimerTriggerComponent>(grenade), Is.True);
            Assert.That(em.HasComponent<GravityWellComponent>(grenade), Is.False);
        });
        await pair.RunSeconds(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(grenade), Is.True);
            Assert.That(server.EntMan.HasComponent<GravityWellComponent>(grenade), Is.False);
        });
        await pair.RunSeconds(2);
        Vector2 initial = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.HasComponent<GravityWellComponent>(grenade), Is.True);
            Assert.That(em.HasComponent<SingularityDistortionComponent>(grenade), Is.True);
            Assert.That(em.GetComponent<TransformComponent>(grenade).Anchored, Is.True);
            item = em.SpawnEntity("Wrench", em.GetComponent<TransformComponent>(grenade).Coordinates.Offset(new Vector2(3, 0)));
            server.System<SharedPhysicsSystem>().WakeBody(item);
            initial = server.System<SharedTransformSystem>().GetWorldPosition(item);
        });
        // Check the initial pull, before an attracted item can cross the center and move away again.
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var transform = server.System<SharedTransformSystem>();
            var center = transform.GetWorldPosition(grenade);
            Assert.That(Vector2.DistanceSquared(transform.GetWorldPosition(item), center),
                Is.LessThan(Vector2.DistanceSquared(initial, center)), "The well must actually attract a nearby movable item.");
        });
        await pair.RunSeconds(8);
        await server.WaitAssertion(() => Assert.That(server.EntMan.EntityExists(grenade), Is.True));
        await pair.RunSeconds(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(grenade), Is.False);
            Assert.That(server.EntMan.EntityExists(item), Is.True, "This grenade attracts items without consuming them.");
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FragileGrenadeIsDestroyedByHeavyDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid grenade = default;
        await server.WaitPost(() =>
        {
            grenade = server.EntMan.SpawnEntity("SingularityGrenade", map.GridCoords);
            server.System<DamageableSystem>().TryChangeDamage(grenade,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 45 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(grenade), Is.False);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Linq;
using System.Numerics;
using Content.Server.Physics.Components;
using Content.Server.Physics.Controllers;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.DeviceLinking.Systems;
using Content.Shared.Lock;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wires;
using Content.Client._IS14.Cyborgs;
using Content.Client.Items;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class CyborgGuidedWeaponTest
{
    [Test]
    public async Task BurstLaserStatusTracksChargeAndRemainingShotsOnTheClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        EntityUid weapon = default;
        EntityUid user = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            user = em.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), user);
            weapon = em.SpawnEntity("IS14CyborgToolXenoWeaponBurstFireLaserXenoborg", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, weapon), Is.True);
            em.RemoveComponent<BatterySelfRechargerComponent>(weapon);
            server.System<SharedBatterySystem>().RefreshChargeRate(weapon);
            server.System<SharedBatterySystem>().SetCharge(weapon, 500);
        });
        await pair.RunTicksSync(10);
        CyborgBatteryStatusControl? status = null;
        await client.WaitAssertion(() =>
        {
            var collect = new ItemStatusCollectMessage();
            client.EntMan.EventBus.RaiseLocalEvent(pair.ToClientUid(weapon), collect);
            status = collect.Controls.OfType<CyborgBatteryStatusControl>().Single();
            Assert.That(status.CurrentData, Is.EqualTo(new CyborgBatteryStatusControl.Data(50, 5, 10)));
        });
        await server.WaitAssertion(() => server.System<SharedBatterySystem>().SetCharge(weapon, 100));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(status!.CurrentData, Is.EqualTo(new CyborgBatteryStatusControl.Data(10, 1, 10)));
            status.Dispose();
        });
        await server.WaitAssertion(() =>
        {
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), null);
            server.EntMan.DeleteEntity(user);
            server.EntMan.DeleteEntity(weapon);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EachBorgGuidesItsOwnLauncherAndTheMissileTurnsTowardsTheMarkedTarget()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var cyborgs = server.System<SharedCyborgSystem>();
            var user = em.SpawnEntity(null, map.GridCoords);
            (EntityUid Borg, EntityUid Launcher, EntityUid Finder) Install()
            {
                var uid = em.SpawnEntity("IS14XenoborgHeavyPrinted", map.GridCoords);
                var borg = em.GetComponent<CyborgComponent>(uid);
                server.System<LockSystem>().Unlock(uid, null);
                Assert.That(server.System<SharedWiresSystem>().TogglePanel(uid,
                    em.GetComponent<WiresPanelComponent>(uid), true));
                var board = em.SpawnEntity("IS14CyborgUpgradeXenoborgGuidedMissileLauncher", map.GridCoords);
                Assert.That(cyborgs.TryInstallUpgrade((uid, borg),
                    (board, em.GetComponent<CyborgUpgradeComponent>(board)), user));
                return (uid, borg.ToolContainer.ContainedEntities.Single(em.HasComponent<TargetAssignComponent>),
                    borg.ToolContainer.ContainedEntities.Single(em.HasComponent<TargetFinderComponent>));
            }

            var first = Install();
            var second = Install();
            foreach (var kit in new[] { first, second })
            {
                Assert.That(em.GetComponent<TargetFinderComponent>(kit.Finder).TargetAssigner, Is.EqualTo(kit.Launcher));
                Assert.That(server.System<DeviceLinkSystem>().GetLinkedSinks(kit.Finder, "TargetSource"),
                    Is.EquivalentTo(new[] { kit.Launcher }));
            }

            var target = em.SpawnEntity("MobHuman",
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(5, 5)));
            var ray = em.SpawnEntity("RangeLaser", map.GridCoords);
            var hit = new HitscanRaycastFiredEvent
            {
                Data = new HitscanRaycastFiredData
                {
                    Gun = first.Finder, HitEntity = target, Hitscan = ray, Shooter = first.Borg
                }
            };
            em.EventBus.RaiseLocalEvent(ray, ref hit);
            Assert.That(em.GetComponent<TargetAssignComponent>(first.Launcher).Target, Is.EqualTo(target));
            Assert.That(em.GetComponent<TargetAssignComponent>(second.Launcher).Target, Is.Null);

            var missile = em.SpawnEntity("BulletGuidedXenoborgMissile", map.GridCoords);
            var shot = new AmmoShotEvent { FiredProjectiles = new() { missile } };
            em.EventBus.RaiseLocalEvent(first.Launcher, shot);
            Assert.That(em.GetComponent<ChasingWalkComponent>(missile).ChasingEntity, Is.EqualTo(target));
            var physics = server.System<SharedPhysicsSystem>();
            physics.SetLinearVelocity(missile, new Vector2(6, 0));
            server.System<ChasingWalkSystem>().UpdateBeforeSolve(false, 0.1f);
            Assert.That(physics.GetLinearVelocity(missile, Vector2.Zero).Y, Is.GreaterThan(0),
                "the missile must steer towards the marked entity rather than fly straight");

            foreach (var uid in new[] { first.Borg, second.Borg, target, ray, missile, user })
                em.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}

using System.Linq;
using System.Numerics;
using Content.Server.Power.Components;
using Content.Shared._IS14.Silicons;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Light.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class AiCameraLightCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestIS14CameraLightCore
  parent: PlayerStationAiEmpty
  components:
  - type: ApcPowerReceiver
    needsPower: false

- type: entity
  id: TestIS14CameraLightCamera
  parent: SurveillanceCameraEngineering
  components:
  - type: ApcPowerReceiver
    needsPower: false
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task RelayedActionPreservesActivityFixturesAndTracksPoweredContacts(bool multipleEyes)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid camera = default, core1 = default, core2 = default, brain1 = default, eye1 = default,
            eye2 = default, observer = default;
        Entity<ActionComponent> action1 = default, action2 = default;
        Fixture originalEyeFixture = null!, originalCameraFixture = null!;
        ICommonSession session = null!;
        EntityUid? previous = null;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            for (var x = -14; x <= 14; x++)
                server.System<SharedMapSystem>().SetTile(map.Grid.Owner, map.Grid.Comp,
                    map.GridCoords.Offset(new Vector2(x, 0)), map.Tile.Tile);
            camera = em.SpawnEntity("TestIS14CameraLightCamera", map.GridCoords);
            core1 = em.SpawnEntity("TestIS14CameraLightCore", map.GridCoords.Offset(new Vector2(10, 0)));
            if (multipleEyes)
                core2 = em.SpawnEntity("TestIS14CameraLightCore", map.GridCoords.Offset(new Vector2(12, 0)));
            originalCameraFixture = em.GetComponent<FixturesComponent>(camera).Fixtures["light"];
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            observer = em.SpawnEntity("MobObserver", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, observer);
        });
        await pair.RunSeconds(1);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<ApcPowerReceiverComponent>(camera).Powered, Is.True);
            (brain1, eye1, action1) = InsertAi(core1);
            server.PlayerMan.SetAttachedEntity(session, brain1);
            originalEyeFixture = em.GetComponent<FixturesComponent>(eye1).Fixtures["lightTrigger"];
            if (multipleEyes)
                (_, eye2, action2) = InsertAi(core2);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<CameraActiveOnCollideComponent>(camera).Enabled, Is.False);
            AssertLight(false);
            Toggle(brain1, action1);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<CameraActiveOnCollideComponent>(camera).Enabled, Is.False,
                "The separate light sensor must not extend the existing activity radius.");
            AssertLight(false);
            Move(eye1, 7);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var body = server.EntMan.GetComponent<PhysicsComponent>(eye1);
            Assert.That(server.EntMan.GetComponent<TransformComponent>(eye1).Coordinates.Position.X, Is.EqualTo(7f).Within(0.01f));
            Assert.That(server.EntMan.GetComponent<CameraActiveOnCollideComponent>(camera).Enabled, Is.True,
                $"Eye awake={body.Awake}, collidable={body.CanCollide}, touching={server.System<SharedPhysicsSystem>().GetTouchingContacts(eye1)}");
            AssertLight(false);
            Move(eye1, 4);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => AssertLight(true));
        var clientCamera = pair.ToClientUid(camera);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(pair.Client.System<SharedPointLightSystem>().TryGetLight(clientCamera, out var light), Is.True);
            Assert.That(light!.Enabled, Is.True);
        });

        if (multipleEyes)
        {
            await server.WaitAssertion(() =>
            {
                Toggle(action2.Comp.AttachedEntity!.Value, action2);
                Move(eye2, -4);
            });
            await pair.RunTicksSync(5);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<LightOnCollideComponent>(camera).Colliders, Has.Count.EqualTo(2));
                Toggle(brain1, action1);
            });
            await pair.RunTicksSync(5);
            await server.WaitAssertion(() =>
            {
                AssertLight(true);
                Assert.That(server.EntMan.GetComponent<LightOnCollideComponent>(camera).Colliders, Has.Count.EqualTo(1));
                server.EntMan.DeleteEntity(core2);
            });
            await pair.RunTicksSync(5);
        }
        else
        {
            await server.WaitPost(() => server.System<SharedPowerReceiverSystem>().SetNeedsPower(camera, true));
            await pair.RunSeconds(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.GetComponent<ApcPowerReceiverComponent>(camera).Powered, Is.False);
                Assert.That(server.EntMan.GetComponent<SlimPoweredLightComponent>(camera).Enabled, Is.True);
                AssertLight(false);
                server.System<SharedPowerReceiverSystem>().SetNeedsPower(camera, false);
            });
            await pair.RunSeconds(1);
            await server.WaitAssertion(() =>
            {
                AssertLight(true);
                Toggle(brain1, action1);
            });
            await pair.RunTicksSync(5);
        }

        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            AssertLight(false);
            Assert.That(em.GetComponent<LightOnCollideComponent>(camera).Colliders, Is.Empty);
            Assert.That(em.HasComponent<LightOnCollideColliderComponent>(eye1), Is.False);
            var fixtures = em.GetComponent<FixturesComponent>(eye1).Fixtures;
            Assert.That(fixtures.ContainsKey("aiLightTrigger"), Is.False);
            Assert.That(fixtures["lightTrigger"], Is.SameAs(originalEyeFixture));
            Assert.That(em.GetComponent<FixturesComponent>(camera).Fixtures["light"], Is.SameAs(originalCameraFixture));
            Move(eye1, 20);
        });
        await pair.RunTicksSync(5);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(pair.Client.System<SharedPointLightSystem>().TryGetLight(clientCamera, out var light), Is.True);
            Assert.That(light!.Enabled, Is.False);
        });
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<CameraActiveOnCollideComponent>(camera).Enabled, Is.False);
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.EntMan.DeleteEntity(observer);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();

        (EntityUid Brain, EntityUid Eye, Entity<ActionComponent> Action) InsertAi(EntityUid core)
        {
            var em = server.EntMan;
            var brain = em.SpawnEntity("StationAiBrain", map.GridCoords);
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(brain, containers.GetContainer(core, StationAiCoreComponent.Container)), Is.True);
            var eye = em.GetComponent<StationAiCoreComponent>(core).RemoteEntity;
            Assert.That(eye, Is.Not.Null);
            var action = server.System<SharedActionsSystem>().GetActions(brain)
                .Single(a => em.GetComponent<MetaDataComponent>(a.Owner).EntityPrototype?.ID == "ActionSurvCameraLights");
            return (brain, eye!.Value, action);
        }

        void Toggle(EntityUid brain, Entity<ActionComponent> action) => server.System<SharedActionsSystem>()
            .PerformAction((brain, server.EntMan.GetComponent<ActionsComponent>(brain)), action);

        void Move(EntityUid eye, int x)
        {
            server.System<SharedTransformSystem>().SetCoordinates(eye, map.GridCoords.Offset(new Vector2(x, 0)));
            server.System<SharedPhysicsSystem>().WakeBody(eye);
        }

        void AssertLight(bool enabled)
        {
            Assert.That(server.System<SharedPointLightSystem>().TryGetLight(camera, out var light), Is.True);
            Assert.That(light!.Enabled, Is.EqualTo(enabled));
        }
    }
}

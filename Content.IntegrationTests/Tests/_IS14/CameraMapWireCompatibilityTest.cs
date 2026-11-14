using System.Linq;
using Content.Server._IS14.SurveillanceCamera;
using Content.Server.SurveillanceCamera;
using Content.Server.Power.Components;
using Content.Server.Wires;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Wires;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class CameraMapWireCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: TestIS14MapWireCamera
          parent: SurveillanceCameraEngineering
          components:
          - type: ApcPowerReceiver
            needsPower: false
        """;

    [Test]
    public async Task CuttingAndMendingMapWireReplicatesVisibilityWithoutDisablingCamera()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid camera = default, human = default, cutters = default;
        ICommonSession session = null!;
        EntityUid? previous = null;
        Wire wire = null!;
        CameraMarker original = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            camera = em.SpawnEntity("TestIS14MapWireCamera", map.GridCoords);
            cutters = em.SpawnEntity("Wirecutter", map.GridCoords);
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            server.PlayerMan.SetAttachedEntity(session, human);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, cutters), Is.True);
            wire = server.System<WiresSystem>().TryGetWires<CameraMapVisibilityWireAction>(camera).Single();
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<ApcPowerReceiverComponent>(camera).Powered, Is.True);
            // The monitor requests a fresh marker after opening; MapInit precedes power startup.
            server.System<SurveillanceCameraMapSystem>().UpdateCameraMarker(
                (camera, server.EntMan.GetComponent<SurveillanceCameraComponent>(camera)));
            original = server.EntMan.GetComponent<SurveillanceCameraMapComponent>(map.Grid.Owner)
                .Cameras[server.EntMan.GetNetEntity(camera)];
            Assert.That(original.Visible, Is.True);
            Assert.That(original.Active, Is.True);
        });
        foreach (var action in new[] { WiresAction.Cut, WiresAction.Mend })
        {
            var visible = action == WiresAction.Mend;
            await server.WaitPost(() => server.EntMan.EventBus.RaiseLocalEvent(camera,
                new WiresActionMessage(wire.Id, action) { Actor = human, UiKey = WiresUiKey.Key }));
            await pair.RunSeconds(1);
            await server.WaitAssertion(() =>
            {
                Assert.That(wire.IsCut, Is.EqualTo(!visible));
                Assert.That(server.System<SurveillanceCameraMapSystem>().IsCameraVisible(camera), Is.EqualTo(visible));
                Assert.That(server.EntMan.GetComponent<SurveillanceCameraComponent>(camera).Active, Is.True);
                var marker = server.EntMan.GetComponent<SurveillanceCameraMapComponent>(map.Grid.Owner)
                    .Cameras[server.EntMan.GetNetEntity(camera)];
                Assert.That(marker.Active, Is.EqualTo(original.Active));
                Assert.That(marker.Address, Is.EqualTo(original.Address));
                Assert.That(marker.Subnet, Is.EqualTo(original.Subnet));
            });
            await pair.Client.WaitAssertion(() =>
            {
                var em = pair.Client.EntMan;
                var marker = em.GetComponent<SurveillanceCameraMapComponent>(pair.ToClientUid(map.Grid.Owner))
                    .Cameras[em.GetNetEntity(pair.ToClientUid(camera))];
                Assert.That(marker.Visible, Is.EqualTo(visible));
                Assert.That(marker.Active, Is.True);
            });
        }
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.EntMan.DeleteEntity(camera);
            server.EntMan.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}

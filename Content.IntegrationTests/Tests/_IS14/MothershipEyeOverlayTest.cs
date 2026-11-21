// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Content.Client.Silicons.StationAi;
using Content.Server._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs;
using Content.Shared.Mind;
using Content.Shared.CCVar;
using Content.Shared.StationAi;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MothershipEyeOverlayTest
{
    [Test]
    public async Task OverlayAppearsOnlyWhileCoreUsesItsEye()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        EntityUid core = default;
        await pair.Server.WaitAssertion(() =>
        {
            core = pair.Server.EntMan.SpawnEntity("MothershipCore", map.GridCoords);
            pair.Server.System<SharedMindSystem>().ControlMob(pair.Server.PlayerMan.Sessions.Single().UserId, core);
        });
        await pair.RunTicksSync(10);
        for (var i = 0; i < 2; i++)
        {
            await pair.Server.WaitAssertion(() =>
            {
                var component = pair.Server.EntMan.GetComponent<MothershipEyeComponent>(core);
                Assert.That(pair.Server.System<MothershipEyeSystem>().Start((core, component)), Is.True);
                Assert.That(pair.Server.EntMan.GetComponent<EyeComponent>(core).DrawFov, Is.False);
                Assert.That(pair.Server.EntMan.GetComponent<EyeComponent>(component.Eye!.Value).DrawFov, Is.False);
            });
            await pair.RunTicksSync(10);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(pair.Client.ResolveDependency<IOverlayManager>().HasOverlay<StationAiOverlay>(), Is.True);
                Assert.That(pair.Client.EntMan.HasComponent<StationAiOverlayComponent>(pair.ToClientUid(core)), Is.False);
                var eye = pair.Client.EntMan.GetComponent<EyeComponent>(pair.ToClientUid(core));
                Assert.That(eye.DrawFov, Is.False, "the client's camera network must not be clipped by walls around the eye");
                Assert.That(eye.Eye.DrawFov, Is.False);
                var config = pair.Client.ResolveDependency<IConfigurationManager>();
                var original = config.GetCVar(CCVars.DisableAiStatic);
                try
                {
                    var overlay = pair.Client.ResolveDependency<IOverlayManager>().GetOverlay<StationAiOverlay>();
                    config.SetCVar(CCVars.DisableAiStatic, false);
                    Assert.That(overlay.StaticShader, Is.EqualTo(new ProtoId<ShaderPrototype>("CameraStatic")));
                    config.SetCVar(CCVars.DisableAiStatic, true);
                    Assert.That(overlay.StaticShader, Is.EqualTo(new ProtoId<ShaderPrototype>("CameraStaticAccessible")),
                        "the mothership eye must use the same accessibility shader as station AI");
                }
                finally
                {
                    config.SetCVar(CCVars.DisableAiStatic, original);
                }
            });
            await pair.Server.WaitPost(() => pair.Server.System<MothershipEyeSystem>().Return(
                (core, pair.Server.EntMan.GetComponent<MothershipEyeComponent>(core))));
            await pair.RunTicksSync(10);
            await pair.Client.WaitAssertion(() =>
                Assert.That(pair.Client.ResolveDependency<IOverlayManager>().HasOverlay<StationAiOverlay>(), Is.False));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PrivateVisionRevealsNearbyTilesWithoutSharingThemWithStationAi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var source = em.SpawnEntity(null, map.GridCoords);
            var grid = new Entity<BroadphaseComponent, MapGridComponent>(map.Grid.Owner,
                em.GetComponent<BroadphaseComponent>(map.Grid.Owner), map.Grid.Comp);
            var bounds = new Box2Rotated(new Box2(-12, -12, 12, 12), Angle.Zero, Vector2.Zero);
            var visible = new HashSet<Vector2i>();
            var vision = pair.Server.System<StationAiVisionSystem>();
            vision.GetView(grid, bounds, visible);
            Assert.That(visible, Is.Empty);

            vision.GetView(grid, bounds, visible, additionalSources:
                new[] { StationAiVisionComponent.CreateViewSource(source, 7f) });
            Assert.That(visible, Does.Contain(Vector2i.Zero));
            Assert.That(visible, Does.Not.Contain(new Vector2i(10, 0)));
            Assert.That(em.HasComponent<StationAiVisionComponent>(source), Is.False);

            visible.Clear();
            vision.GetView(grid, bounds, visible);
            Assert.That(visible, Is.Empty, "private mothership sources must not become station AI cameras");

            // A second source must reveal its room even when a wall hides it from the first source.
            var maps = pair.Server.System<SharedMapSystem>();
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords.Offset(new Vector2(x, y)), map.Tile.Tile);
            var walls = new List<EntityUid>();
            for (var y = -8; y <= 8; y++)
                walls.Add(em.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(1, y))));
            var remote = em.SpawnEntity(null, map.GridCoords.Offset(new Vector2(3, 0)));
            var behindWall = new Vector2i(3, 0);
            visible.Clear();
            vision.GetView(grid, bounds, visible, additionalSources:
                new[] { StationAiVisionComponent.CreateViewSource(source, 7f) });
            Assert.That(visible, Does.Not.Contain(behindWall), "the wall must block this source");
            visible.Clear();
            vision.GetView(grid, bounds, visible, additionalSources: new[]
            {
                StationAiVisionComponent.CreateViewSource(source, 7f),
                StationAiVisionComponent.CreateViewSource(remote, 7f)
            });
            Assert.That(visible, Does.Contain(behindWall), "the remote source must reveal tiles behind the wall");
            em.DeleteEntity(remote);
            foreach (var wall in walls)
                em.DeleteEntity(wall);
            em.DeleteEntity(source);
        });
        await pair.CleanReturnAsync();
    }
}

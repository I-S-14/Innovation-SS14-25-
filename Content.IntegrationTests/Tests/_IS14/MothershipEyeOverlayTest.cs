// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Content.Client.Silicons.StationAi;
using Content.Server._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs;
using Content.Shared.Mind;
using Content.Shared.StationAi;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Client.Graphics;

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
            });
            await pair.RunTicksSync(10);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(pair.Client.ResolveDependency<IOverlayManager>().HasOverlay<StationAiOverlay>(), Is.True);
                Assert.That(pair.Client.EntMan.HasComponent<StationAiOverlayComponent>(pair.ToClientUid(core)), Is.False);
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
            em.DeleteEntity(source);
        });
        await pair.CleanReturnAsync();
    }
}

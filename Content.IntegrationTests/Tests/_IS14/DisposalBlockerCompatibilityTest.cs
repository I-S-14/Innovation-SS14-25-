using System.Numerics;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DisposalBlockerCompatibilityTest
{
    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public async Task BlockerConnectsBothEndsAndForcesTheOriginalGoobExit(int degrees)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var maps = server.System<SharedMapSystem>();
            var transform = server.System<SharedTransformSystem>();
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp,
                    map.GridCoords.Offset(new Vector2(x, y)), map.Tile.Tile);
            var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            var rotation = Angle.FromDegrees(degrees);
            // Accepted Goob's OnGetTransitConnectableDirections used rotation.GetDir()
            // first and its opposite second. Its blocker always chose the first exit.
            var forward = rotation.GetDir();
            var backward = forward.GetOpposite();
            var firstOffset = forward.ToIntVec();
            var secondOffset = backward.ToIntVec();
            var blocker = em.SpawnEntity("DisposalBlocker", coords);
            var first = em.SpawnEntity("DisposalPipe", coords.Offset(new Vector2(firstOffset.X, firstOffset.Y)));
            var second = em.SpawnEntity("DisposalPipe", coords.Offset(new Vector2(secondOffset.X, secondOffset.Y)));
            foreach (var uid in new[] { blocker, first, second })
                transform.SetLocalRotation(uid, rotation);
            var tubes = server.System<DisposalTubeSystem>();
            var blockerTube = em.GetComponent<DisposalTubeComponent>(blocker);
            Assert.That(tubes.GetTubeInDirection((first, em.GetComponent<DisposalTubeComponent>(first)), backward),
                Is.EqualTo(blocker), "One side must not become a disconnected pipe end.");
            Assert.That(tubes.GetTubeInDirection((second, em.GetComponent<DisposalTubeComponent>(second)), forward),
                Is.EqualTo(blocker), "Both physical ports must remain connectable.");
            Assert.That(tubes.GetTubeInDirection((blocker, blockerTube), forward), Is.EqualTo(first));
            Assert.That(tubes.GetTubeInDirection((blocker, blockerTube), backward), Is.EqualTo(second));
            var holder = em.SpawnEntity("DisposalHolder", coords);
            var direction = new GetDisposalsNextDirectionEvent((holder, em.GetComponent<DisposalHolderComponent>(holder)));
            em.EventBus.RaiseLocalEvent(blocker, ref direction);
            Assert.That(direction.Handled, Is.True);
            Assert.That(direction.Next, Is.EqualTo(forward));
            foreach (var uid in new[] { holder, blocker, first, second })
                em.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}

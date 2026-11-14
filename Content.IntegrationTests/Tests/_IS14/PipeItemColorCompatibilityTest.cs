using System.Linq;
using Content.Client._IS14.Atmos;
using Content.Client.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.EntitySystems;
using Content.Shared._IS14.Atmos;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class PipeItemColorCompatibilityTest
{
    [Test]
    public async Task PaintedPipeKeepsItsColorInHandsStorageIconsAndAfterDropping()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, pipe = default;
        ICommonSession session = null!;
        EntityUid? previous = null;
        var colors = new[] { Color.Red, Color.FromHex("#2CA8DA") };
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            pipe = em.SpawnEntity("GasPipeStraight", map.GridCoords);
            server.System<SharedTransformSystem>().Unanchor(pipe);
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            server.PlayerMan.SetAttachedEntity(session, human);
            server.System<AtmosPipeColorSystem>().SetColor(pipe, em.GetComponent<AtmosPipeColorComponent>(pipe), colors[0]);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, pipe), Is.True);
        });
        foreach (var expected in colors)
        {
            await server.WaitPost(() => server.System<AtmosPipeColorSystem>()
                .SetColor(pipe, server.EntMan.GetComponent<AtmosPipeColorComponent>(pipe), expected));
            await pair.RunTicksSync(5);
            await pair.Client.WaitAssertion(() =>
            {
                var em = pair.Client.EntMan;
                var clientPipe = pair.ToClientUid(pipe);
                var clientHuman = pair.ToClientUid(human);
                Assert.That(em.GetComponent<AtmosPipeColorComponent>(clientPipe).Color, Is.EqualTo(expected));
                Assert.That(em.GetComponent<SpriteComponent>(clientPipe)[PipeVisualLayers.Pipe].Color.WithAlpha(1f), Is.EqualTo(expected));
                Assert.That(em.GetComponent<ItemComponent>(clientPipe).StoredSprite, Is.Not.Null,
                    "This pipe uses the stored-sprite rendering path rather than a rendered world entity.");
                var tint = new StoredItemColorEvent();
                em.EventBus.RaiseLocalEvent(clientPipe, tint);
                Assert.That(tint.Color, Is.EqualTo(expected));
                var heldLayers = em.GetComponent<HandsComponent>(clientHuman).RevealedLayers.Values.SelectMany(v => v).ToArray();
                Assert.That(heldLayers, Is.Not.Empty);
                var humanSprite = em.GetComponent<SpriteComponent>(clientHuman);
                foreach (var key in heldLayers)
                    Assert.That(humanSprite[key].Color, Is.EqualTo(expected), key);
            });
        }
        await server.WaitAssertion(() => Assert.That(server.System<SharedHandsSystem>().TryDrop(human, pipe, map.GridCoords), Is.True));
        await pair.RunTicksSync(5);
        await pair.Client.WaitAssertion(() => Assert.That(
            pair.Client.EntMan.GetComponent<SpriteComponent>(pair.ToClientUid(pipe))[PipeVisualLayers.Pipe].Color.WithAlpha(1f),
            Is.EqualTo(colors[1])));
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}

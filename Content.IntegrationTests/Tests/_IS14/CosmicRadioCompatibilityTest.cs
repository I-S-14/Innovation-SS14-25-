using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio.Components;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class CosmicRadioCompatibilityTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task CosmicMessagesCrossMapsOnlyForSubscribedListeners(bool subscribed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var receiverMap = await pair.CreateTestMap();
        var sourceMap = await pair.CreateTestMap();
        EntityUid speaker = default;
        ICommonSession session = null!;
        EntityUid? previous = null;
        var message = $"IS14 cosmic radio compatibility {subscribed}";
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var receiver = em.SpawnEntity("MobHuman", receiverMap.GridCoords);
            speaker = em.SpawnEntity("MobHuman", sourceMap.GridCoords);
            var radio = em.EnsureComponent<ActiveRadioComponent>(receiver);
            radio.Channels.Clear();
            radio.Channels.Add(subscribed ? "CosmicRadio" : "Common");
            em.EnsureComponent<IntrinsicRadioReceiverComponent>(receiver);
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            server.PlayerMan.SetAttachedEntity(session, receiver);
        });
        await pair.RunTicksSync(5);
        await server.WaitPost(() => server.System<RadioSystem>().SendRadioMessage(speaker, message, "CosmicRadio", speaker));
        await pair.RunTicksSync(10);
        await pair.Client.WaitAssertion(() =>
        {
            var chat = pair.Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
            Assert.That(chat.History.Any(h => h.Msg.Message.Contains(message)), Is.EqualTo(subscribed),
                "The retained long-range channel must reach its listener without leaking into Common.");
        });
        await server.WaitPost(() =>
        {
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.System<SharedMapSystem>().DeleteMap(receiverMap.MapId);
            server.System<SharedMapSystem>().DeleteMap(sourceMap.MapId);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}

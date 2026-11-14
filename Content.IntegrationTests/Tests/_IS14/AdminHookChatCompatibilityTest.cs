using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class AdminHookChatCompatibilityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(true)]
    [TestCase(false)]
    public async Task ExternalAdminChatOnlyReachesActiveAdmins(bool activeAdmin)
    {
        var privateMessage = $"admin-hook-private-{Guid.NewGuid()}";
        var publicMessage = $"admin-hook-control-{Guid.NewGuid()}";
        await Server.WaitPost(() =>
        {
            var session = Server.PlayerMan.Sessions.Single();
            var admins = Server.ResolveDependency<IAdminManager>();
            if (!admins.IsAdmin(session, includeDeAdmin: true))
                admins.PromoteHost(session);
        });
        await PoolManager.WaitUntil(Server, () => Server.ResolveDependency<IAdminManager>()
            .IsAdmin(Server.PlayerMan.Sessions.Single(), includeDeAdmin: true));
        await Server.WaitAssertion(() =>
        {
            var session = Server.PlayerMan.Sessions.Single();
            var admins = Server.ResolveDependency<IAdminManager>();
            if (activeAdmin)
                admins.ReAdmin(session);
            else
                admins.DeAdmin(session);

            Assert.That(admins.ActiveAdmins.Contains(session), Is.EqualTo(activeAdmin));
            var chat = Server.ResolveDependency<IChatManager>();
            chat.SendHookAdmin("integration-test", privateMessage);
            chat.SendHookOOC("integration-test", publicMessage);
        });

        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() =>
        {
            var history = Client.ResolveDependency<IUserInterfaceManager>()
                .GetUIController<ChatUIController>().History;
            Assert.That(history.Any(entry => entry.Msg.Message == publicMessage), Is.True,
                "The control message must arrive so that lack of private chat cannot be a transport failure.");
            Assert.That(history.Any(entry => entry.Msg.Channel == ChatChannel.AdminChat
                && entry.Msg.Message == privateMessage), Is.EqualTo(activeAdmin));
        });
    }
}

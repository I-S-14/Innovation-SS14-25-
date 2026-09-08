// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Globalization;
using System.Linq;
using Content.Client._IS14.OS;
using Content.IntegrationTests.Pair;
using Content.Server._IS14.OS.Apps;
using Content.Server.GameTicking;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Inventory;
using Content.Shared.PDA.Ringer;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The chat notification a carried device puts in its owner's chat, and the reply link on
///     it. The link is the only way into the OS from outside its own window, so it is also the
///     only place where a claim about which device to open comes from the client.
/// </summary>
[TestFixture]
public sealed class OsMessengerNotifyTest
{
    private const string LocId = "is14-os-messenger-chat-notification";

    [Test]
    public async Task ReplyLinkOpensTheConversation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var ticker = server.System<GameTicker>();

        await server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);

        var player = pair.Player!.AttachedEntity!.Value;
        var entMan = server.EntMan;
        var inv = server.System<InventorySystem>();

        EntityUid mine = default;
        EntityUid theirs = default;
        var myAddress = string.Empty;
        var theirAddress = string.Empty;
        var rang = false;
        var rangWhileMuted = true;

        await server.WaitPost(() =>
        {
            Assert.That(inv.TryGetSlotEntity(player, "id", out var slot), "no PDA in the id slot");
            mine = slot!.Value;

            // A second device, on the floor next to them, to send from.
            theirs = entMan.SpawnEntity("IS14_PdaAssistant", entMan.GetComponent<TransformComponent>(player).Coordinates);
        });

        await pair.RunTicksSync(10);

        await server.WaitPost(() =>
        {
            myAddress = entMan.GetComponent<DeviceNetworkComponent>(mine).Address!;
            theirAddress = entMan.GetComponent<DeviceNetworkComponent>(theirs).Address!;

            Assert.That(myAddress, Is.Not.Empty, "the worn PDA never got a network address");
            Assert.That(theirAddress, Is.Not.Empty, "the spawned PDA never got a network address");

            var ringer = entMan.GetComponent<RingerComponent>(mine);
            var mineMessenger = entMan.GetComponent<IS14OsMessengerComponent>(mine);
            var theirMessenger = entMan.GetComponent<IS14OsMessengerComponent>(theirs);

            // Silent mode goes first, while nothing has rung yet: a ringer that is already
            // going cannot be told apart from one that was never silenced.
            mineMessenger.Muted = true;
            Send(entMan, theirs, myAddress, "тук-тук");
            rangWhileMuted = ringer.Active;

            mineMessenger.Muted = false;
            theirMessenger.LastSend = TimeSpan.Zero;

            // Markup in the body would be a way to forge chat lines, so send some.
            Send(entMan, theirs, myAddress, "[color=#ff0000]жду[/color]");

            // Read in here rather than after the ticks: the ringtone is six notes long and
            // would already be over by then.
            rang = ringer.Active;
        });

        await pair.RunTicksSync(5);

        var messenger = entMan.GetComponent<IS14OsMessengerComponent>(mine);
        var device = entMan.GetComponent<IS14OsDeviceComponent>(mine);

        Assert.That(messenger.Chats.ContainsKey(theirAddress), "the message never arrived");

        Assert.Multiple(() =>
        {
            Assert.That(rang, "a new message did not play the ringtone the owner set");
            Assert.That(rangWhileMuted, Is.False, "silent mode did not stop the ringtone");
        });

        Assert.That(messenger.OpenChat, Is.Null, "the conversation was already open before the reply");

        // The notification the owner sees. Built here the same way the system builds it, so a
        // broken locale string shows up as a failing test rather than a mangled chat line.
        var netDevice = entMan.GetNetEntity(mine).Id.ToString(CultureInfo.InvariantCulture);
        var loc = server.ResolveDependency<ILocalizationManager>();
        var wrapped = loc.GetString(LocId,
            ("name", FormattedMessage.EscapeText("Урист МакПочтальон")),
            ("message", FormattedMessage.EscapeText("[color=#ff0000]жду[/color]")),
            ("device", netDevice),
            ("address", theirAddress));

        Assert.That(FormattedMessage.TryFromMarkup(wrapped, out var parsed, out var error), Is.True,
            $"the notification is not valid markup: {error}");

        var link = parsed!.Nodes.FirstOrDefault(n => n.Name == "is14reply");

        Assert.Multiple(() =>
        {
            Assert.That(link.Name, Is.EqualTo("is14reply"), "the notification carries no reply link");
            Assert.That(link.Attributes["device"].StringValue, Is.EqualTo(netDevice),
                "the reply link points at the wrong device");
            Assert.That(link.Attributes["chat"].StringValue, Is.EqualTo(theirAddress),
                "the reply link points at the wrong conversation");

            // The sender's markup has to survive as text, not as formatting.
            Assert.That(parsed.ToString(), Does.Contain("[color=#ff0000]жду[/color]"),
                "a message body was parsed as markup instead of being escaped");
            Assert.That(parsed.Nodes.Any(n => n.Name == "color" && n.Value.ColorValue == Color.FromHex("#ff0000")),
                Is.False,
                "a colour tag typed into a message took effect in the chat line");
        });

        // Press the link.
        await client.WaitPost(() =>
            client.System<IS14OsNotificationSystem>().RequestOpenChat(entMan.GetNetEntity(mine), theirAddress));

        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(messenger.OpenChat, Is.EqualTo(theirAddress), "the reply link did not open the conversation");
            Assert.That(device.Open, Does.Contain(new ProtoId<IS14OsAppPrototype>(
                    IS14OsMessengerSystem.AppId)),
                "the messenger window was not opened");
            Assert.That(messenger.Chats[theirAddress].Unread, Is.False, "the conversation stayed unread");
        });

        // The same request naming a device the player is not carrying: the link is markup, and
        // markup in chat can be typed by anyone.
        var loose = entMan.GetComponent<IS14OsMessengerComponent>(theirs);
        var looseDevice = entMan.GetComponent<IS14OsDeviceComponent>(theirs);

        await client.WaitPost(() =>
            client.System<IS14OsNotificationSystem>().RequestOpenChat(entMan.GetNetEntity(theirs), myAddress));

        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(loose.OpenChat, Is.Null, "a device on the floor was opened by someone else's reply link");
            Assert.That(looseDevice.Open, Does.Not.Contain(new ProtoId<IS14OsAppPrototype>(
                    IS14OsMessengerSystem.AppId)),
                "a device on the floor had its messenger opened remotely");
        });

        await pair.CleanReturnAsync();
    }

    private static void Send(IEntityManager entMan, EntityUid from, string to, string text)
    {
        var ev = new OsAppEventRaised(
            IS14OsMessengerSystem.AppId,
            new OsMessengerSendEvent(to, text, null),
            from);

        entMan.EventBus.RaiseLocalEvent(from, ref ev);
    }
}

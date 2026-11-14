using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private string WrapAnnouncement(string sender, string message, string? signature)
    {
        var escapedMessage = FormattedMessage.EscapeText(message);
        return string.IsNullOrWhiteSpace(signature)
            ? Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", escapedMessage))
            : Loc.GetString("chat-manager-sender-announcement-wrap-message-signed", ("sender", sender), ("message", escapedMessage), ("signature", FormattedMessage.EscapeText(signature)));
    }
}

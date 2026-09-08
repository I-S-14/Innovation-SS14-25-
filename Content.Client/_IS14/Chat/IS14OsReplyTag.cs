// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Client._IS14.OS;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Chat;

/// <summary>
///     Draws <c>[is14reply="Reply" device="42" chat="A3F1"/]</c> as a link that opens the
///     device's messenger on that conversation.
///
///     Rich text can host a control mid-line, so the button lives in the chat message itself
///     rather than in a separate window — the notification and the way to act on it are the
///     same line of text.
///
///     The device is carried as a plain string rather than a number because the tag is written
///     from a locale file, and a number placeable would be formatted for the reader's locale
///     (a thousands separator in the middle of an entity id). Nothing here is trusted: chat
///     markup is parsed with every tag allowed, so anyone could type this tag naming any
///     device. The server is what checks the claim.
/// </summary>
public sealed class IS14OsReplyTag : IMarkupTagHandler
{
    public const string TagName = "is14reply";

    private static readonly Color Link = Color.FromHex("#7BC8F0");
    private static readonly Color LinkHover = Color.FromHex("#B4E4FF");

    [Dependency] private readonly IEntitySystemManager _sysMan = default!;

    public string Name => TagName;

    /// <inheritdoc/>
    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!node.Value.TryGetString(out var text)
            || !node.Attributes.TryGetValue("device", out var deviceParam)
            || !deviceParam.TryGetString(out var deviceRaw)
            || !int.TryParse(deviceRaw, out var deviceId)
            || !node.Attributes.TryGetValue("chat", out var chatParam)
            || !chatParam.TryGetString(out var address))
        {
            return false;
        }

        var device = new NetEntity(deviceId);

        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterMode.Stop,
            FontColorOverride = Link,
            DefaultCursorShape = Control.CursorShape.Hand,
        };

        label.OnMouseEntered += _ => label.FontColorOverride = LinkHover;
        label.OnMouseExited += _ => label.FontColorOverride = Link;

        label.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            // Looked up on the click, not when the tag is built: rich text is also drawn
            // before the game is running, and the systems do not exist then.
            if (_sysMan.TryGetEntitySystem<IS14OsNotificationSystem>(out var notifications))
                notifications.RequestOpenChat(device, address);

            args.Handle();
        };

        control = label;
        return true;
    }
}

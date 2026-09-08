// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._IS14.Chat;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Chat;

/// <summary>
///     Draws <c>[emoji=id]</c> as the sprite named by the emoji prototype.
///
///     Rich text already knows how to put a control in the middle of a line, so an emoji is
///     just a small <see cref="TextureRect"/> — which means any texture or RSI state in the
///     game can be a smiley without new art, and emoji work in every rich-text surface that
///     allows the tag.
/// </summary>
public sealed class IS14EmojiTag : IMarkupTagHandler
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;

    public string Name => IS14EmojiSystem.TagName;

    /// <inheritdoc/>
    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (node.Value.StringValue is not { } id
            || !_proto.TryIndex<IS14EmojiPrototype>(id, out var emoji)
            || !_sysMan.TryGetEntitySystem<SpriteSystem>(out var sprites))
        {
            // An unknown emoji, or rich text drawn before the game is running: draw nothing
            // rather than taking the message down with it.
            return false;
        }

        control = new TextureRect
        {
            Texture = sprites.Frame0(emoji.Sprite),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(emoji.Size, emoji.Size),
            VerticalAlignment = Control.VAlignment.Center,
        };

        return true;
    }
}

/// <summary>
///     Tag sets for surfaces that show emoji. Rich text only honours the tags it is handed,
///     and the engine's default set does not include ours — nor is that set reachable from
///     content, so the safe formatting tags are spelled out here.
/// </summary>
public static class IS14EmojiText
{
    /// <summary>The safe formatting tags, plus emoji.</summary>
    public static readonly Type[] Tags =
    {
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
        typeof(IS14EmojiTag),
    };
}

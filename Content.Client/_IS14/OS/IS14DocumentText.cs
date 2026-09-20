// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.RichText;
using Robust.Client.UserInterface.RichText;

namespace Content.Client._IS14.OS;

/// <summary>
///     The formatting a document written on a device may use.
///
///     Every surface that shows a document has to be handed the same set, or the editor and
///     the reader disagree about what a file looks like — a tag the editor offers but the
///     reader does not allow shows up as raw brackets in the Explorer. That is the whole
///     reason this list is in one place instead of at each call site.
///
///     It is the engine's default set plus monospace. The other tags in the game are
///     deliberately left out: <c>font size</c> would let one document set its own text to any
///     size on someone else's screen, and <c>scramble</c> generates noise rather than
///     formatting the text it is given, so neither is something a writing tool should offer.
/// </summary>
public static class IS14DocumentText
{
    public static readonly Type[] Tags =
    {
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
        typeof(MonoTag),
    };
}

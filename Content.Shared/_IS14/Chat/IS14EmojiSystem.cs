using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Chat;

/// <summary>
///     The emoji registry: which shortcodes exist and what typed text turns into.
///
///     Lives in Shared on purpose. Only the client can draw a sprite, but deciding that ":)"
///     is an emoji is the same decision everywhere, so a server-side chat that wants smileys
///     later calls the same <see cref="Format"/> instead of growing its own copy of the table.
/// </summary>
public sealed class IS14EmojiSystem : EntitySystem
{
    /// <summary>Markup tag the client renders. Text never becomes anything else.</summary>
    public const string TagName = "emoji";

    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly Dictionary<string, IS14EmojiPrototype> _codes = new();
    private readonly List<IS14EmojiPrototype> _picker = new();

    /// <summary>First characters of every shortcode, so ordinary text costs one lookup a char.</summary>
    private readonly HashSet<char> _starts = new();

    private int _longest;

    /// <summary>Emoji a picker should offer, in display order.</summary>
    public IReadOnlyList<IS14EmojiPrototype> Picker => _picker;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);
        Cache();
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<IS14EmojiPrototype>())
            Cache();
    }

    private void Cache()
    {
        _codes.Clear();
        _picker.Clear();
        _starts.Clear();
        _longest = 0;

        foreach (var emoji in _proto.EnumeratePrototypes<IS14EmojiPrototype>())
        {
            foreach (var code in emoji.Codes)
            {
                if (code.Length == 0)
                    continue;

                _codes[code] = emoji;
                _starts.Add(code[0]);
                _longest = Math.Max(_longest, code.Length);
            }

            if (emoji.InPicker)
                _picker.Add(emoji);
        }

        _picker.Sort(static (a, b) => a.Order != b.Order
            ? a.Order.CompareTo(b.Order)
            : string.Compare(a.ID, b.ID, StringComparison.Ordinal));
    }

    /// <summary>What the picker inserts into the text box for this emoji.</summary>
    public string PrimaryCode(IS14EmojiPrototype emoji)
    {
        return emoji.Codes.Count > 0 ? emoji.Codes[0] : string.Empty;
    }

    /// <summary>
    ///     Turns typed text into a message with emoji in it.
    ///
    ///     The text is never parsed as markup — only shortcodes become nodes, everything else
    ///     is added as literal text. That is the whole security story: a player who types
    ///     "[color=red]" gets those nine characters, not a coloured message, and no amount of
    ///     bracket-juggling reaches the tags the UI actually honours.
    /// </summary>
    public FormattedMessage Format(string text)
    {
        var message = new FormattedMessage();

        var run = 0;
        var i = 0;

        while (i < text.Length)
        {
            if (!TryMatch(text, i, out var emoji, out var length))
            {
                i++;
                continue;
            }

            if (i > run)
                message.AddText(text[run..i]);

            message.PushTag(new MarkupNode(TagName, new MarkupParameter(emoji.ID), null), selfClosing: true);

            i += length;
            run = i;
        }

        if (run < text.Length)
            message.AddText(text[run..]);

        return message;
    }

    /// <summary>Longest match wins, so "&gt;:(" beats the ":(" sitting inside it.</summary>
    private bool TryMatch(string text, int start, out IS14EmojiPrototype emoji, out int length)
    {
        emoji = default!;
        length = 0;

        if (!_starts.Contains(text[start]))
            return false;

        var max = Math.Min(_longest, text.Length - start);

        for (var size = max; size > 0; size--)
        {
            if (!_codes.TryGetValue(text.Substring(start, size), out var match))
                continue;

            emoji = match;
            length = size;
            return true;
        }

        return false;
    }
}

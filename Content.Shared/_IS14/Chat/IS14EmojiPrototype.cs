using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Chat;

/// <summary>
///     One emoji: the shortcodes people type for it and the sprite it turns into.
///
///     Deliberately a prototype rather than a table in code — adding a smiley should be a YAML
///     file and nothing else, and the sprite is a <see cref="SpriteSpecifier"/> so any texture
///     or RSI state already in the game can be pressed into service without copying art.
/// </summary>
[Prototype("emoji")]
public sealed partial class IS14EmojiPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     What the player types. Several per emoji is normal (":)" and ":smile:"), and codes
    ///     are matched longest-first, so ">:(" wins over ":(" even though both would fit.
    /// </summary>
    [DataField(required: true)]
    public List<string> Codes = new();

    /// <summary>Any texture or RSI state in the game. Animated states render their first frame.</summary>
    [DataField(required: true)]
    public SpriteSpecifier Sprite = default!;

    /// <summary>Shown in the picker's tooltip next to the shortcode.</summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    ///     Drawn size in virtual pixels. Bigger than a line of text on purpose: the art sits
    ///     inside a 32x32 frame with room to spare, so the face is smaller than the number here.
    ///     Rich text pushes the following line down for a tall emoji, so this is safe to raise.
    /// </summary>
    [DataField]
    public int Size = 22;

    /// <summary>False keeps it typeable but out of the picker — aliases, joke codes, secrets.</summary>
    [DataField]
    public bool InPicker = true;

    /// <summary>Picker order. Equal orders fall back to the prototype id.</summary>
    [DataField]
    public int Order;
}

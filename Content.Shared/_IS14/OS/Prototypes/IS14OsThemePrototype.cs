using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.OS.Prototypes;

/// <summary>
///     A theme is pure data: the shell controls paint themselves from it at runtime, so
///     switching themes never touches an engine stylesheet. See Docs/_IS14/os-design.md §8.
/// </summary>
[Prototype("osTheme")]
public sealed partial class IS14OsThemePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public Color Background = Color.FromHex("#0E1218");

    [DataField]
    public Color Panel = Color.FromHex("#161C24");

    [DataField]
    public Color PanelAlt = Color.FromHex("#1E2732");

    [DataField]
    public Color Border = Color.FromHex("#2B3949");

    [DataField]
    public Color Accent = Color.FromHex("#4FB3E0");

    [DataField]
    public Color Text = Color.FromHex("#D2DBE5");

    [DataField]
    public Color TextDim = Color.FromHex("#7E8B9B");

    [DataField]
    public Color Good = Color.FromHex("#63C68C");

    [DataField]
    public Color Bad = Color.FromHex("#E06767");

    [DataField]
    public Color Warning = Color.FromHex("#D9A44F");

    /// <summary>Monospace shell font. Terminal-flavoured themes turn this on.</summary>
    [DataField]
    public bool Monospace;

    [DataField]
    public ResPath? Wallpaper;

    /// <summary>Requires being found or downloaded rather than being available by default.</summary>
    [DataField]
    public bool Unlockable;
}

using Content.Client._IS14.Controls;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._IS14.OS.Shell;

/// <summary>
///     Turns an osTheme prototype into the palette our shared controls understand, and holds
///     the shell's own glyphs. Apps get their icon from their prototype; these are the pieces
///     of chrome that belong to the OS itself.
/// </summary>
public static class IS14OsStyle
{
    private const string Verb = "/Textures/Interface/VerbIcons/";
    private const string Nano = "/Textures/Interface/Nano/";
    private const string Emote = "/Textures/Interface/Emotes/";
    private const string Action = "/Textures/Interface/Actions/";

    public static readonly SpriteSpecifier Logo = New(Nano + "ntlogo.svg.png");
    public static readonly SpriteSpecifier Clock = New(Verb + "clock.svg.192dpi.png");
    public static readonly SpriteSpecifier Alert = New(Verb + "zap.svg.192dpi.png");
    public static readonly SpriteSpecifier Close = New(Verb + "close.svg.192dpi.png");
    public static readonly SpriteSpecifier Minimize = New(Verb + "fold.svg.192dpi.png");
    public static readonly SpriteSpecifier Fallback = New(Verb + "dot.svg.192dpi.png");

    public static readonly SpriteSpecifier Owner = New(Verb + "sentient.svg.192dpi.png");
    public static readonly SpriteSpecifier Job = New(Verb + "outfit.svg.192dpi.png");
    public static readonly SpriteSpecifier Station = New(Verb + "anchor.svg.192dpi.png");
    public static readonly SpriteSpecifier Address = New(Verb + "debug.svg.192dpi.png");
    public static readonly SpriteSpecifier Memory = New(Verb + "group.svg.192dpi.png");
    public static readonly SpriteSpecifier Delete = New(Verb + "delete.svg.192dpi.png");
    public static readonly SpriteSpecifier Light = New(Verb + "light.svg.192dpi.png");
    public static readonly SpriteSpecifier Ringtone = New(Emote + "chime.png");
    public static readonly SpriteSpecifier Uplink = New(Action + "shop.png");
    public static readonly SpriteSpecifier Lock = New(Verb + "lock.svg.192dpi.png");
    public static readonly SpriteSpecifier Theme = New(Verb + "settings.svg.192dpi.png");
    public static readonly SpriteSpecifier Save = New(Verb + "insert.svg.192dpi.png");
    public static readonly SpriteSpecifier Download = New(Verb + "in.svg.192dpi.png");
    public static readonly SpriteSpecifier Battery = New(Verb + "zap.svg.192dpi.png");
    public static readonly SpriteSpecifier Manifest = New(Action + "manifest.png");
    public static readonly SpriteSpecifier Store = New(Action + "shop.png");

    private static SpriteSpecifier New(string path)
    {
        return new SpriteSpecifier.Texture(new ResPath(path));
    }

    public static Texture? Resolve(SpriteSystem sprites, SpriteSpecifier? specifier)
    {
        return specifier == null ? null : sprites.Frame0(specifier);
    }

    /// <summary>
    ///     Theme colours mapped onto the shared palette. Themes only carry the roles that
    ///     matter to them, so the raised-panel and bright-border roles are derived rather than
    ///     asking every theme author to pick nine near-identical greys.
    /// </summary>
    public static IS14ThemePalette ToPalette(IS14OsThemePrototype theme)
    {
        return new IS14ThemePalette
        {
            Backdrop = theme.Background,
            Panel = theme.Panel,
            PanelRaised = theme.PanelAlt,
            Border = theme.Border,
            BorderBright = theme.Accent,
            Accent = theme.Accent,
            Good = theme.Good,
            Warn = theme.Warning,
            Bad = theme.Bad,
            Muted = theme.TextDim,
            Text = theme.Text,
        };
    }
}

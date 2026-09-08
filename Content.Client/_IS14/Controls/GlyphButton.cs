// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A window-chrome button: a cross for closing, a bar for minimising.
///
///     The mark is a texture in a child <see cref="TextureRect"/> rather than something drawn
///     in <see cref="Control.Draw"/>. That is deliberate — the drawn version rendered nothing
///     here — and it costs nothing, because the engine already ships both shapes as flat
///     single-colour images that tint cleanly to whatever the theme wants.
/// </summary>
public sealed class GlyphButton : ContainerButton
{
    public enum Mark : byte
    {
        /// <summary>An X, for closing.</summary>
        Cross,

        /// <summary>A single bar, for minimising.</summary>
        Bar,
    }

    private const string CrossPath = "/Textures/Interface/Nano/cross.svg.png";
    private const string BarPath = "/Textures/Interface/Changelog/minus.svg.192dpi.png";

    private readonly TextureRect _mark;

    private IS14ThemePalette _palette = IS14ThemePalette.Default;
    private Mark _glyph = Mark.Cross;

    public Mark Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            _mark.Texture = Load(value);
        }
    }

    /// <summary>Side of the box the mark is fitted into, in virtual pixels.</summary>
    public float GlyphSize
    {
        get => _mark.SetSize.X;
        set => _mark.SetSize = new Vector2(value, value);
    }

    /// <summary>
    ///     Paints the hover state with the palette's bad colour — the standard way a close
    ///     button says what it is about to do.
    /// </summary>
    public bool Danger { get; set; }

    public IS14ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            DrawModeChanged();
        }
    }

    public GlyphButton()
    {
        _mark = new TextureRect
        {
            Texture = Load(_glyph),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(14, 14),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        AddChild(_mark);

        MinSize = new Vector2(26, 22);
        DrawModeChanged();
    }

    private static Texture Load(Mark mark)
    {
        var cache = IoCManager.Resolve<IResourceCache>();

        return cache.GetTexture(mark == Mark.Bar ? BarPath : CrossPath);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // The base constructor gets here before the mark is built.
        if (_mark == null!)
            return;

        var (background, stroke) = DrawMode switch
        {
            DrawModeEnum.Pressed => (Danger ? _palette.Bad : _palette.Accent.WithAlpha(0.35f), _palette.Text),
            DrawModeEnum.Hover => (Danger ? _palette.Bad.WithAlpha(0.85f) : _palette.PanelRaised, _palette.Text),
            DrawModeEnum.Disabled => (Color.Transparent, _palette.Muted.WithAlpha(0.5f)),
            _ => (Color.Transparent, _palette.Muted),
        };

        StyleBoxOverride = new StyleBoxFlat { BackgroundColor = background };
        _mark.ModulateSelfOverride = stroke;
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A small labelled button for toolbars: one or two characters rather than a word, in its
///     own font if the caller wants one.
///
///     A formatting bar has to show what it does — the bold button is bold, the italic button
///     leans — and that means a per-button font, which <see cref="IconTile"/> deliberately does
///     not offer because its captions belong to the theme. This is the other half of that pair:
///     no icon, no wrapping, just a glyph that can be latched on with <see cref="Selected"/>
///     when it stands for a mode instead of an action.
/// </summary>
public sealed class ToolButton : ContainerButton
{
    private IS14ThemePalette _palette = IS14ThemePalette.Default;
    private bool _selected;

    private readonly Label _label;

    public string? Text
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>Draws the caption in this font instead of the theme's. Null keeps the default.</summary>
    public Font? Font
    {
        get => _label.FontOverride;
        set => _label.FontOverride = value;
    }

    /// <summary>Overrides the caption colour — a swatch button names its own colour this way.</summary>
    public Color? TextColor
    {
        get => _label.FontColorOverride;
        set => _label.FontColorOverride = value ?? _palette.Text;
    }

    /// <summary>Latched on: drawn as held down, for a button that stands for a mode.</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            DrawModeChanged();
        }
    }

    public IS14ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            _label.FontColorOverride = value.Text;
            DrawModeChanged();
        }
    }

    public ToolButton()
    {
        _label = new Label
        {
            Align = Label.AlignMode.Center,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
            FontColorOverride = _palette.Text,
        };

        AddChild(_label);

        MinSize = new Vector2(22, 20);
        DrawModeChanged();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // The base constructor runs before the label field is assigned.
        if (_label == null!)
            return;

        var mode = DrawMode;
        if (_selected && mode == DrawModeEnum.Normal)
            mode = DrawModeEnum.Pressed;

        var (background, border) = mode switch
        {
            DrawModeEnum.Pressed => (_palette.Accent.WithAlpha(0.30f), _palette.Accent),
            DrawModeEnum.Hover => (_palette.PanelRaised, _palette.BorderBright),
            DrawModeEnum.Disabled => (_palette.Panel.WithAlpha(0.5f), _palette.Border),
            _ => (_palette.Panel, _palette.Border),
        };

        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 4,
            ContentMarginRightOverride = 4,
            ContentMarginTopOverride = 1,
            ContentMarginBottomOverride = 1,
        };
    }
}

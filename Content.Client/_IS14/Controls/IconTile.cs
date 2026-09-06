// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A pressable icon with a caption, in either a stacked tile or a compact row.
///
///     This is the workhorse for any list of things the player picks from by recognising a
///     picture: application shortcuts and taskbar entries in the OS, but equally anything else
///     that wants "icon plus one word" instead of a wall of buttons. It paints itself from an
///     <see cref="IS14ThemePalette"/>, so a skinnable surface can restyle a whole screenful of
///     them by handing over a new palette.
/// </summary>
public sealed class IconTile : ContainerButton
{
    private IS14ThemePalette _palette = IS14ThemePalette.Default;
    private bool _selected;
    private bool _badge;

    private readonly BoxContainer _layout;
    private readonly TextureRect _icon;
    private readonly PanelContainer _swatch;
    private readonly Label _caption;
    private readonly PanelContainer _badgeDot;
    private Color? _swatchColor;

    /// <summary>Stacked (icon above caption) reads as a desktop shortcut; compact reads as a row.</summary>
    public bool Compact
    {
        get => _layout.Orientation == BoxContainer.LayoutOrientation.Horizontal;
        set
        {
            _layout.Orientation = value
                ? BoxContainer.LayoutOrientation.Horizontal
                : BoxContainer.LayoutOrientation.Vertical;

            _icon.Margin = value ? new Thickness(0, 0, 6, 0) : new Thickness(0, 0, 0, 3);
            _swatch.Margin = _icon.Margin;
            _caption.HorizontalAlignment = value ? HAlignment.Left : HAlignment.Center;
            _caption.VerticalAlignment = value ? VAlignment.Center : VAlignment.Top;
            _caption.Align = value ? Label.AlignMode.Left : Label.AlignMode.Center;
        }
    }

    public Texture? Icon
    {
        get => _icon.Texture;
        set
        {
            _icon.Texture = value;
            _icon.Visible = value != null;
        }
    }

    public string? Caption
    {
        get => _caption.Text;
        set
        {
            _caption.Text = value;
            _caption.Visible = !string.IsNullOrEmpty(value);
        }
    }

    public float IconSize
    {
        get => _icon.SetSize.X;
        set
        {
            _icon.SetSize = new Vector2(value, value);
            _swatch.MinSize = new Vector2(value, value);
        }
    }

    /// <summary>
    ///     A flat colour block in place of the icon. Themes and paint jobs are better shown
    ///     than named, and this keeps that out of the caller's layout code.
    /// </summary>
    public Color? Swatch
    {
        get => _swatchColor;
        set
        {
            _swatchColor = value;
            _swatch.Visible = value != null;

            if (value != null)
            {
                _swatch.PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = value.Value,
                    BorderColor = _palette.Border,
                    BorderThickness = new Thickness(1),
                };
            }
        }
    }

    /// <summary>
    ///     Cut the caption off instead of letting the tile grow. Off by default: a clipped word
    ///     is worse than a wide button, so callers opt in only where width is truly fixed.
    /// </summary>
    public bool ClipCaption
    {
        get => _caption.ClipText;
        set => _caption.ClipText = value;
    }

    /// <summary>Latched on: drawn as held down, for the entry that is currently in front.</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            DrawModeChanged();
        }
    }

    /// <summary>A small accent dot, for "this one wants your attention".</summary>
    public bool Badge
    {
        get => _badge;
        set
        {
            _badge = value;
            _badgeDot.Visible = value;
        }
    }

    public IS14ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            ApplyPalette();
        }
    }

    public IconTile()
    {
        _icon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(20, 20),
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };

        _swatch = new PanelContainer
        {
            MinSize = new Vector2(20, 20),
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };

        _caption = new Label
        {
            Align = Label.AlignMode.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _badgeDot = new PanelContainer
        {
            MinSize = new Vector2(6, 6),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Top,
            Margin = new Thickness(0, 2, 2, 0),
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };

        _layout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _layout.AddChild(_icon);
        _layout.AddChild(_swatch);
        _layout.AddChild(_caption);

        AddChild(_layout);
        AddChild(_badgeDot);

        ApplyPalette();
    }

    private void ApplyPalette()
    {
        // Verb icons are black silhouettes, so they have to be tinted or they read as holes.
        _icon.ModulateSelfOverride = _palette.Text;
        _caption.FontColorOverride = _palette.Text;

        if (_swatchColor != null)
            Swatch = _swatchColor;

        _badgeDot.PanelOverride = new StyleBoxFlat { BackgroundColor = _palette.Accent };
        DrawModeChanged();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // The base constructor gets here before our fields are assigned.
        if (_palette == null!)
            return;

        var mode = DrawMode;
        if (_selected && mode == DrawModeEnum.Normal)
            mode = DrawModeEnum.Pressed;

        var (background, border) = mode switch
        {
            DrawModeEnum.Pressed => (_palette.Accent.WithAlpha(0.26f), _palette.Accent),
            DrawModeEnum.Hover => (_palette.PanelRaised, _palette.Accent.WithAlpha(0.7f)),
            DrawModeEnum.Disabled => (_palette.Panel.WithAlpha(0.5f), _palette.Border),
            _ => (_palette.PanelRaised, _palette.Border),
        };

        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 5,
            ContentMarginRightOverride = 5,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        Modulate = mode == DrawModeEnum.Disabled ? new Color(1f, 1f, 1f, 0.55f) : Color.White;
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A pressable picture with a caption under it, for grids of images: a photo gallery, a
///     wallpaper picker, anything the player chooses by looking rather than by reading.
///
///     Deliberately not an <see cref="IconTile"/>: that one tints its icon with the theme's
///     text colour, which is right for a black silhouette glyph and ruinous for a photograph.
///     Here the image is shown as it is, and the frame does the theming.
/// </summary>
public sealed class PictureTile : ContainerButton
{
    private IS14ThemePalette _palette = IS14ThemePalette.Default;
    private bool _selected;

    private readonly TextureRect _picture;
    private readonly Label _placeholder;
    private readonly Label _caption;

    /// <summary>Shown while the picture is still on its way, or unreadable.</summary>
    public string? Placeholder
    {
        get => _placeholder.Text;
        set => _placeholder.Text = value;
    }

    public Texture? Picture
    {
        get => _picture.Texture;
        set
        {
            _picture.Texture = value;
            _picture.Visible = value != null;
            _placeholder.Visible = value == null;
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

    /// <summary>Side of the square the image is fitted into, in virtual pixels.</summary>
    public float PictureSize
    {
        get => _picture.SetSize.X;
        set
        {
            _picture.SetSize = new Vector2(value, value);
            _placeholder.SetSize = new Vector2(value, value);
        }
    }

    /// <summary>Latched on: drawn as held down, for the picture currently being viewed.</summary>
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
            _caption.FontColorOverride = value.Muted;
            _placeholder.FontColorOverride = value.Muted;
            DrawModeChanged();
        }
    }

    public PictureTile()
    {
        _picture = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(72, 72),
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };

        _placeholder = new Label
        {
            Align = Label.AlignMode.Center,
            VerticalAlignment = VAlignment.Center,
            SetSize = new Vector2(72, 72),
            MouseFilter = MouseFilterMode.Ignore,
            FontColorOverride = _palette.Muted,
        };

        _caption = new Label
        {
            Align = Label.AlignMode.Center,
            ClipText = true,
            MouseFilter = MouseFilterMode.Ignore,
            FontColorOverride = _palette.Muted,
            Visible = false,
        };

        var layout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        layout.AddChild(_picture);
        layout.AddChild(_placeholder);
        layout.AddChild(_caption);

        AddChild(layout);
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
            _ => (_palette.Backdrop, _palette.Border),
        };

        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 3,
            ContentMarginRightOverride = 3,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };
    }
}

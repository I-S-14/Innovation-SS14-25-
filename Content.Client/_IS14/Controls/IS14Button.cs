// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     A button that paints itself from the IS14 palette instead of the station
///     stylesheet, so the whole readout keeps one look without editing upstream styles.
///     Used for everything the player can press here: the paper-doll tiles, the module
///     switches and the two big deploy controls.
/// </summary>
public sealed class IS14Button : ContainerButton
{
    private Color _accent = IS14Palette.Accent;
    private bool _selected;

    /// <summary>
    ///     Colour the button lights up in. Green for engaged, amber for a warning,
    ///     cyan for anything neutral.
    /// </summary>
    public Color Accent
    {
        get => _accent;
        set
        {
            _accent = value;
            DrawModeChanged();
        }
    }

    /// <summary>
    ///     Latched on: drawn as though held down, for toggles that are currently engaged.
    /// </summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            DrawModeChanged();
        }
    }

    private float _padding = 6f;

    /// <summary>Padding inside the button's frame.</summary>
    public float Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            DrawModeChanged();
            InvalidateMeasure();
        }
    }

    public IS14Button()
    {
        DrawModeChanged();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // The base constructor reaches this before our fields exist.
        if (_accent == default)
            _accent = IS14Palette.Accent;

        var mode = DrawMode;

        if (_selected && mode == DrawModeEnum.Normal)
            mode = DrawModeEnum.Pressed;

        var (background, border) = mode switch
        {
            DrawModeEnum.Pressed => (_accent.WithAlpha(0.28f), _accent),
            DrawModeEnum.Hover => (IS14Palette.PanelRaised, _accent.WithAlpha(0.75f)),
            DrawModeEnum.Disabled => (IS14Palette.Panel.WithAlpha(0.5f), IS14Palette.Border),
            _ => (IS14Palette.PanelRaised, IS14Palette.Border),
        };

        StyleBoxOverride = IS14Palette.Box(background, border, 1f, _padding);

        Modulate = mode == DrawModeEnum.Disabled ? new Color(1f, 1f, 1f, 0.55f) : Color.White;
    }

    /// <summary>
    ///     Builds the standard "icon then caption" button. Either half may be omitted:
    ///     an icon alone is how the compact controls in the module list are drawn.
    /// </summary>
    public static IS14Button Make(
        Texture? icon,
        string? text,
        Color accent,
        float iconSize = 20f,
        float padding = 6f)
    {
        var button = new IS14Button
        {
            Accent = accent,
            Padding = padding,
        };

        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
        };

        if (icon != null)
        {
            var rect = IS14Palette.Icon(icon, iconSize);

            if (text != null)
                rect.Margin = new Thickness(0, 0, 6, 0);

            stack.AddChild(rect);
        }

        if (text != null)
        {
            stack.AddChild(new Label
            {
                Text = text,
                VerticalAlignment = VAlignment.Center,
                FontColorOverride = IS14Palette.Text,
            });
        }

        button.AddChild(stack);
        return button;
    }

    /// <summary>
    ///     The oversized controls along the bottom of the readout.
    /// </summary>
    public static IS14Button Big(Texture? icon, string text, Color accent)
    {
        var button = Make(icon, text, accent, iconSize: 32f, padding: 8f);
        button.HorizontalExpand = true;
        return button;
    }

    /// <summary>
    ///     Swaps the icon and caption of a button built by <see cref="Make"/>.
    /// </summary>
    public void SetContent(Texture? icon, string? text)
    {
        foreach (var child in Children)
        {
            foreach (var grandchild in child.Children)
            {
                switch (grandchild)
                {
                    case Label label when text != null:
                        label.Text = text;
                        break;
                    case TextureRect rect when icon != null:
                        rect.Texture = icon;
                        break;
                }
            }
        }
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     One line of a readout: icon, what it is, and what it currently says.
///
///     Rows of plain labels are the fastest way to make a screen unreadable — the eye has
///     nothing to anchor on. Giving each line a glyph and pinning the value to the right turns
///     the same data into something scannable, and the trailing slot takes a button when the
///     line is also actionable.
/// </summary>
public sealed class StatRow : PanelContainer
{
    private IS14ThemePalette _palette = IS14ThemePalette.Default;

    private readonly TextureRect _icon;
    private readonly Label _caption;
    private readonly Label _value;
    private readonly BoxContainer _trailing;

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
        set => _caption.Text = value;
    }

    public string? Value
    {
        get => _value.Text;
        set
        {
            _value.Text = value;
            _value.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>Overrides the value colour — alert levels and warnings use this.</summary>
    public Color? ValueColor
    {
        get => _value.FontColorOverride;
        set => _value.FontColorOverride = value ?? _palette.Text;
    }

    /// <summary>Draws a panel behind the row. Off by default so dense lists stay flat.</summary>
    public bool Framed
    {
        get => PanelOverride != null;
        set => PanelOverride = value
            ? new StyleBoxFlat
            {
                BackgroundColor = _palette.Panel,
                BorderColor = _palette.Border,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            }
            : null;
    }

    public IS14ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            _icon.ModulateSelfOverride = value.Muted;
            _caption.FontColorOverride = value.Muted;
            _value.FontColorOverride = value.Text;

            if (Framed)
                Framed = true;
        }
    }

    public StatRow()
    {
        _icon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(16, 16),
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            MouseFilter = MouseFilterMode.Ignore,
            ModulateSelfOverride = _palette.Muted,
            Visible = false,
        };

        _caption = new Label
        {
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = _palette.Muted,
        };

        // ClipText makes a Label measure as zero wide (engine Label.MeasureOverride), so the
        // value can only survive if it claims the leftover space itself: hand it the expand and
        // right-align the text inside it, rather than pinning a zero-width control to the right
        // of a spacer that has already eaten the row.
        _value = new Label
        {
            VerticalAlignment = VAlignment.Center,
            Align = Label.AlignMode.Right,
            HorizontalExpand = true,
            Margin = new Thickness(8, 0, 0, 0),
            ClipText = true,
            FontColorOverride = _palette.Text,
            Visible = false,
        };

        _trailing = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        row.AddChild(_icon);
        row.AddChild(_caption);
        row.AddChild(_value);
        row.AddChild(_trailing);

        AddChild(row);
    }

    /// <summary>Puts a control (usually a button) at the end of the row.</summary>
    public void SetTrailing(Control? control)
    {
        _trailing.RemoveAllChildren();

        if (control != null)
            _trailing.AddChild(control);
    }
}

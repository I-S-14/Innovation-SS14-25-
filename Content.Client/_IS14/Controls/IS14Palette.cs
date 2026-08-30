// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     The shared look for IS14 interfaces that should read as hardware rather than as
///     station software: dark ground, thin bright borders, one accent.
///
///     Everything here is plain style boxes built at runtime, so no upstream stylesheet
///     has to be touched. Domains with their own identity — the Gosplan consoles, for
///     one — keep their own palette and pass its colours into these controls instead.
/// </summary>
public static class IS14Palette
{
    public static readonly Color Backdrop = Color.FromHex("#0E1218");
    public static readonly Color Panel = Color.FromHex("#161C24");
    public static readonly Color PanelRaised = Color.FromHex("#1E2732");
    public static readonly Color Border = Color.FromHex("#2B3949");
    public static readonly Color BorderBright = Color.FromHex("#3E566E");

    public static readonly Color Accent = Color.FromHex("#4FB3E0");
    public static readonly Color Good = Color.FromHex("#63C68C");
    public static readonly Color Warn = Color.FromHex("#D9A44F");
    public static readonly Color Bad = Color.FromHex("#E06767");
    public static readonly Color Muted = Color.FromHex("#7E8B9B");
    public static readonly Color Text = Color.FromHex("#D2DBE5");

    /// <summary>
    ///     A flat inset panel: the background every readout block sits on.
    /// </summary>
    public static StyleBoxFlat Box(Color background, Color border, float thickness = 1f, float padding = 0f)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
        };

        if (padding > 0f)
            box.SetContentMarginOverride(StyleBox.Margin.All, padding + thickness);

        return box;
    }

    /// <summary>
    ///     Wraps content in a bordered block, the way every section of the readout is drawn.
    /// </summary>
    public static PanelContainer Section(Control content, float padding = 8f)
    {
        var panel = new PanelContainer
        {
            PanelOverride = Box(Panel, Border, 1f, padding),
        };

        panel.AddChild(content);
        return panel;
    }

    /// <summary>
    ///     A one-word status pill — the readout's way of shouting without using red text.
    /// </summary>
    public static Control Chip(string text, Color color)
    {
        var panel = new PanelContainer
        {
            PanelOverride = Box(color.WithAlpha(0.16f), color, 1f, 3f),
            VerticalAlignment = Control.VAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };

        panel.AddChild(new Label
        {
            Text = text,
            FontColorOverride = color,
            StyleClasses = { "LabelSubText" },
            Margin = new Thickness(4, 0),
        });

        return panel;
    }

    /// <summary>
    ///     A pill whose unit is a glyph rather than a word. Watts and complexity get
    ///     printed on every card in a dense list, and spelling them out turns the card
    ///     into a sentence; the symbol says the same thing in a quarter of the width.
    /// </summary>
    public static Control IconChip(Texture? icon, string text, Color color, string? tooltip = null)
    {
        var panel = new PanelContainer
        {
            PanelOverride = Box(color.WithAlpha(0.16f), color, 1f, 3f),
            VerticalAlignment = Control.VAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = tooltip,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 0),
        };

        row.AddChild(new Label
        {
            Text = text,
            FontColorOverride = color,
            StyleClasses = { "LabelSubText" },
        });

        var glyph = Icon(icon, 12f, color);
        glyph.Margin = new Thickness(3, 0, 0, 0);
        row.AddChild(glyph);

        panel.AddChild(row);
        return panel;
    }

    /// <summary>
    ///     A thin rule used to separate stacked readouts.
    /// </summary>
    public static Control Rule(float margin = 6f) => new PanelContainer
    {
        PanelOverride = new StyleBoxFlat(Border),
        MinHeight = 1,
        Margin = new Thickness(0, margin),
    };

    /// <summary>
    ///     Verb icons ship as black silhouettes, so every one of them is tinted on the way
    ///     in — otherwise the whole readout would be studded with black holes.
    /// </summary>
    public static TextureRect Icon(Texture? texture, float size = 20f, Color? color = null) => new()
    {
        Texture = texture,
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
        SetSize = new Vector2(size, size),
        ModulateSelfOverride = color ?? Text,
        VerticalAlignment = Control.VAlignment.Center,
        MouseFilter = Control.MouseFilterMode.Ignore,
    };

    public static Label Heading(string text) => new()
    {
        Text = text,
        FontColorOverride = Accent,
        StyleClasses = { "LabelKeyText" },
    };

    public static Label Sub(string text, Color? color = null) => new()
    {
        Text = text,
        FontColorOverride = color ?? Muted,
        StyleClasses = { "LabelSubText" },
    };
}

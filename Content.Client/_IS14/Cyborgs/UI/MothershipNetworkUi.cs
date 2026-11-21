// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Cyborgs.UI;

/// <summary>Shared visual language for the mothership's observation and navigation terminals.</summary>
internal static class MothershipNetworkUi
{
    public static readonly Color Accent = Color.FromHex("#78DCE8");
    public static readonly Color Muted = Color.FromHex("#94A9BD");
    public static readonly Color Online = Color.FromHex("#92DFBA");

    public static BoxContainer Column(int separation = 8) => new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = separation,
        HorizontalExpand = true
    };

    public static BoxContainer Row(int separation = 8) => new()
    {
        Orientation = BoxContainer.LayoutOrientation.Horizontal,
        SeparationOverride = separation,
        HorizontalExpand = true
    };

    public static Label Caption(string text, Color? color = null) => new()
    {
        Text = text,
        FontColorOverride = color ?? Muted,
        HorizontalExpand = true,
        ClipText = true,
        ToolTip = text,
    };

    public static TextureRect Icon(string state, int size = 24) => new()
    {
        Texture = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().GetFrame(
            new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Actions/actions_borg.rsi"), state), TimeSpan.Zero),
        SetSize = new Vector2(size, size),
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
        MouseFilter = Control.MouseFilterMode.Ignore,
    };

    public static Control Portrait(NetEntity target, string fallback)
    {
        var em = IoCManager.Resolve<IEntityManager>();
        if (em.TryGetEntity(target, out var uid) && em.HasComponent<SpriteComponent>(uid))
        {
            var sprite = new SpriteView { SetSize = new Vector2(48, 48), MouseFilter = Control.MouseFilterMode.Ignore };
            sprite.SetEntity(uid);
            return sprite;
        }
        return Icon(fallback, 48);
    }

    public static PanelContainer Panel(Control contents, bool selected = false)
    {
        contents.Margin = new Thickness(12, 10);
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex(selected ? "#19333F" : "#151F2B"),
                BorderColor = selected ? Accent : Color.FromHex("#30465B"),
                BorderThickness = new Thickness(selected ? 3 : 1, 1, 1, 1),
            }
        };
        panel.AddChild(contents);
        return panel;
    }

    public static Control Header(string icon, string title, string subtitle)
    {
        var row = Row(14);
        row.AddChild(Icon(icon, 48));
        var labels = Column(4);
        var heading = Caption(Loc.GetString(title), Accent);
        heading.AddStyleClass("LabelHeading");
        labels.AddChild(heading);
        labels.AddChild(Caption(Loc.GetString(subtitle)));
        row.AddChild(labels);
        return Panel(row);
    }

    public static Button Button(string text, string icon, string? tooltip = null)
    {
        var button = new Button
        {
            Text = Loc.GetString(text),
            ToolTip = tooltip == null ? null : Loc.GetString(tooltip),
            HorizontalExpand = true,
            MinHeight = 36,
        };
        button.Label.Margin = new Thickness(28, 0, 0, 0);
        var image = Icon(icon, 24);
        image.HorizontalAlignment = Control.HAlignment.Left;
        image.VerticalAlignment = Control.VAlignment.Center;
        button.AddChild(image);
        return button;
    }

    public static void Empty(BoxContainer list, bool filtered)
    {
        var body = Column(8);
        var icon = Icon("xenoborg-camera-computer", 40);
        icon.HorizontalAlignment = Control.HAlignment.Center;
        icon.ModulateSelfOverride = Muted;
        body.AddChild(icon);
        body.AddChild(Caption(Loc.GetString(filtered ? "mothership-network-no-matches" : "mothership-network-no-signals")));
        list.AddChild(Panel(body));
    }

    public static void Footer(BoxContainer contents, string text)
    {
        var label = new RichTextLabel { Margin = new Thickness(2, 6), ModulateSelfOverride = Muted };
        label.SetMessage(Loc.GetString(text));
        contents.AddChild(label);
    }
}

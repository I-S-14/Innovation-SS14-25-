using Content.Client.Pinpointer.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Economy.EconomyMonitor;

public sealed class EconomyMonitorNavMapControl : NavMapControl
{
    public NetEntity? FocusVendor;
    public Dictionary<NetEntity, string> LocalizedNames = new();

    private readonly Label _focusLabel;
    private readonly PanelContainer _focusPanel;

    public EconomyMonitorNavMapControl() : base()
    {
        WallColor = new Color(80, 180, 130);
        TileColor = new Color(20, 55, 40);
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(BackgroundOpacity));

        _focusLabel = new Label
        {
            Margin = new Thickness(8f, 6f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Modulate = Color.White,
        };

        _focusPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = BackgroundColor },
            Margin = new Thickness(4f, 8f),
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Bottom,
            Visible = false,
        };

        _focusPanel.AddChild(_focusLabel);
        AddChild(_focusPanel);

        // The base NavMapControl hardcodes a 650px-wide top panel (zoom/beacons/recenter),
        // which overflows our narrow side column — let it size to its content instead.
        if (ChildCount > 0 && GetChild(0) is BoxContainer { ChildCount: > 0 } topContainer
            && topContainer.GetChild(0) is PanelContainer topPanel)
        {
            topPanel.SetWidth = float.NaN;
            topPanel.HorizontalExpand = false;
            topPanel.HorizontalAlignment = HAlignment.Left;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (FocusVendor == null || !TrackedEntities.ContainsKey(FocusVendor.Value))
        {
            _focusPanel.Visible = false;
            return;
        }

        _focusLabel.Text = LocalizedNames.TryGetValue(FocusVendor.Value, out var name)
            ? name
            : Loc.GetString("economy-monitor-unknown-vendor");

        _focusPanel.Visible = true;
    }
}

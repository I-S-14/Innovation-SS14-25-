// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.Controls;
using Content.Shared._IS14.Pinpointer;
using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._IS14.Cyborgs.UI;

public sealed class MothershipPinpointerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MothershipPinpointerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MothershipPinpointerWindow>();
        _window.Selected += target => SendMessage(new MothershipPinpointerSelectMessage(target));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is MothershipPinpointerState pointer)
            _window?.UpdateState(pointer);
    }
}

public sealed class MothershipPinpointerWindow : FancyWindow
{
    private readonly BoxContainer _targets = MothershipNetworkUi.Column();
    private readonly Label _status = MothershipNetworkUi.Caption(string.Empty, MothershipNetworkUi.Accent);
    private readonly Label _count = MothershipNetworkUi.Caption(string.Empty);
    private readonly LineEdit _search = new() { PlaceHolder = Loc.GetString("mothership-network-search") };
    private readonly Button _off = MothershipNetworkUi.Button("mothership-pinpointer-off", "no-action");
    private MothershipPinpointerState? _state;
    public event Action<NetEntity?>? Selected;

    public MothershipPinpointerWindow()
    {
        Title = Loc.GetString("mothership-pinpointer-title");
        MinWidth = 480;
        MinHeight = 380;
        SetSize = new System.Numerics.Vector2(540, 500);
        var contents = MothershipNetworkUi.Column(10);
        ContentsContainer.AddChild(contents);
        contents.AddChild(MothershipNetworkUi.Header("gps-module", "mothership-network-brand", "mothership-pinpointer-subtitle"));
        var status = MothershipNetworkUi.Column(4);
        status.AddChild(MothershipNetworkUi.Caption(Loc.GetString("mothership-pinpointer-uplink")));
        status.AddChild(_status);
        contents.AddChild(MothershipNetworkUi.Panel(status, true));
        contents.AddChild(_off);
        _off.OnPressed += _ => Selected?.Invoke(null);
        contents.AddChild(_count);
        contents.AddChild(_search);
        _search.OnTextChanged += _ => Populate();
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        contents.AddChild(scroll);
        scroll.AddChild(_targets);
        MothershipNetworkUi.Footer(contents, "mothership-pinpointer-hint");
    }

    public void UpdateState(MothershipPinpointerState state)
    {
        var name = state.Targets.Find(target => target.Entity == state.Selected).Name;
        _status.Text = !state.Active ? Loc.GetString("mothership-pinpointer-disabled")
            : name != null ? Loc.GetString("mothership-pinpointer-tracking", ("name", name))
            : Loc.GetString("mothership-pinpointer-unavailable");
        _status.ToolTip = _status.Text;
        _status.FontColorOverride = state.Active && name != null ? MothershipNetworkUi.Online : MothershipNetworkUi.Muted;
        _off.Disabled = !state.Active;
        _count.Text = Loc.GetString("mothership-pinpointer-targets", ("count", state.Targets.Count));
        var changed = _state == null || _state.Active != state.Active || _state.Selected != state.Selected ||
                      !_state.Targets.SequenceEqual(state.Targets);
        _state = state;
        if (changed)
            Populate();
    }

    private void Populate()
    {
        _targets.RemoveAllChildren();
        if (_state == null)
            return;
        foreach (var (entity, label) in _state.Targets)
        {
            if (!label.Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase))
                continue;
            var selected = _state.Active && _state.Selected == entity;
            var row = MothershipNetworkUi.Row(12);
            row.AddChild(MothershipNetworkUi.Portrait(entity, "gps-module"));
            var info = MothershipNetworkUi.Column(4);
            info.AddChild(MothershipNetworkUi.Caption(label, Color.White));
            info.AddChild(MothershipNetworkUi.Caption(Loc.GetString(selected ? "mothership-pinpointer-locked" : "mothership-network-signal"),
                selected ? MothershipNetworkUi.Accent : MothershipNetworkUi.Muted));
            row.AddChild(info);
            var button = MothershipNetworkUi.Button(selected ? "mothership-pinpointer-selected" : "mothership-pinpointer-acquire", "gps-module");
            button.HorizontalExpand = false;
            button.Disabled = selected;
            button.OnPressed += _ => Selected?.Invoke(entity);
            row.AddChild(button);
            _targets.AddChild(MothershipNetworkUi.Panel(row, selected));
        }
        if (_targets.ChildCount == 0)
            MothershipNetworkUi.Empty(_targets, !string.IsNullOrWhiteSpace(_search.Text));
    }
}

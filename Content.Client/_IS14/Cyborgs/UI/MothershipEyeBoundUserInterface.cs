// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.Controls;
using Content.Shared._IS14.Cyborgs;
using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._IS14.Cyborgs.UI;

public sealed class MothershipEyeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MothershipEyeWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MothershipEyeWindow>();
        _window.Command += (command, target) => SendMessage(new MothershipEyeMessage(command, target));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is MothershipEyeState eye)
            _window?.UpdateState(eye);
    }
}

public sealed class MothershipEyeWindow : FancyWindow
{
    private readonly Label _status = MothershipNetworkUi.Caption(string.Empty, MothershipNetworkUi.Accent);
    private readonly Label _count = MothershipNetworkUi.Caption(string.Empty);
    private readonly BoxContainer _borgs = MothershipNetworkUi.Column();
    private readonly LineEdit _search = new() { PlaceHolder = Loc.GetString("mothership-network-search") };
    private readonly Button _return;
    private MothershipEyeState? _state;
    public event Action<MothershipEyeCommand, NetEntity?>? Command;

    public MothershipEyeWindow()
    {
        Title = Loc.GetString("mothership-eye-title");
        MinWidth = 600;
        MinHeight = 400;
        SetSize = new System.Numerics.Vector2(660, 540);
        var contents = MothershipNetworkUi.Column(10);
        ContentsContainer.AddChild(contents);
        contents.AddChild(MothershipNetworkUi.Header("xenoborg-camera-computer", "mothership-network-brand", "mothership-eye-subtitle"));
        var status = MothershipNetworkUi.Column(4);
        status.AddChild(MothershipNetworkUi.Caption(Loc.GetString("mothership-eye-mode")));
        status.AddChild(_status);
        contents.AddChild(MothershipNetworkUi.Panel(status, true));
        var controls = MothershipNetworkUi.Row();
        contents.AddChild(controls);
        var ship = MothershipNetworkUi.Button("mothership-eye-ship", "xenoborg-eye-module", "mothership-eye-ship-hint");
        ship.OnPressed += _ => Command?.Invoke(MothershipEyeCommand.Ship, null);
        controls.AddChild(ship);
        _return = MothershipNetworkUi.Button("mothership-eye-return", "xenoborg-module-module", "mothership-eye-return-hint");
        _return.OnPressed += _ => Command?.Invoke(MothershipEyeCommand.Return, null);
        controls.AddChild(_return);
        contents.AddChild(_count);
        contents.AddChild(_search);
        _search.OnTextChanged += _ => Populate();
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        contents.AddChild(scroll);
        scroll.AddChild(_borgs);
        MothershipNetworkUi.Footer(contents, "mothership-eye-hint");
    }

    public void UpdateState(MothershipEyeState state)
    {
        _status.Text = state.Following is { } following
            ? Loc.GetString("mothership-eye-following", ("name", following))
            : Loc.GetString(state.Active ? "mothership-eye-free" : "mothership-eye-home");
        _status.ToolTip = _status.Text;
        _return.Disabled = !state.Active;
        _count.Text = Loc.GetString("mothership-eye-channels", ("count", state.Borgs.Count));
        var changed = _state == null || _state.Following != state.Following || !_state.Borgs.SequenceEqual(state.Borgs);
        _state = state;
        if (changed)
            Populate();
    }

    private void Populate()
    {
        _borgs.RemoveAllChildren();
        if (_state == null)
            return;
        var channel = 0;
        foreach (var (entity, name) in _state.Borgs)
        {
            channel++;
            if (!name.Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase))
                continue;
            var selected = _state.Following == name;
            var row = MothershipNetworkUi.Row(12);
            row.AddChild(MothershipNetworkUi.Portrait(entity, "xenoborg-camera-computer"));
            var info = MothershipNetworkUi.Column(3);
            info.AddChild(MothershipNetworkUi.Caption(Loc.GetString("mothership-eye-channel", ("number", channel)), MothershipNetworkUi.Accent));
            info.AddChild(MothershipNetworkUi.Caption(name, Color.White));
            info.AddChild(MothershipNetworkUi.Caption(Loc.GetString(selected ? "mothership-eye-linked" : "mothership-network-signal"), MothershipNetworkUi.Online));
            row.AddChild(info);
            var actions = MothershipNetworkUi.Column(4);
            actions.HorizontalExpand = false;
            var jump = MothershipNetworkUi.Button("mothership-eye-jump", "xenoborg-eye-module", "mothership-eye-jump-hint");
            jump.OnPressed += _ => Command?.Invoke(MothershipEyeCommand.Jump, entity);
            var follow = MothershipNetworkUi.Button("mothership-eye-follow", "xenoborg-eye2-module", "mothership-eye-follow-hint");
            follow.Disabled = selected;
            follow.OnPressed += _ => Command?.Invoke(MothershipEyeCommand.Follow, entity);
            actions.AddChild(jump);
            actions.AddChild(follow);
            row.AddChild(actions);
            _borgs.AddChild(MothershipNetworkUi.Panel(row, selected));
        }
        if (_borgs.ChildCount == 0)
            MothershipNetworkUi.Empty(_borgs, !string.IsNullOrWhiteSpace(_search.Text));
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.Controls;
using Content.Shared._IS14.Pinpointer;
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
    private readonly BoxContainer _targets = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _status = new();
    private readonly Button _off = new() { Text = Loc.GetString("mothership-pinpointer-off") };
    public event Action<NetEntity?>? Selected;

    public MothershipPinpointerWindow()
    {
        Title = Loc.GetString("mothership-pinpointer-title");
        MinWidth = 380;
        MinHeight = 260;
        var contents = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        ContentsContainer.AddChild(contents);
        contents.AddChild(_status);
        contents.AddChild(_off);
        _off.OnPressed += _ => Selected?.Invoke(null);
        var scroll = new ScrollContainer { VerticalExpand = true };
        contents.AddChild(scroll);
        scroll.AddChild(_targets);
    }

    public void UpdateState(MothershipPinpointerState state)
    {
        var name = state.Targets.Find(target => target.Entity == state.Selected).Name;
        _status.Text = !state.Active ? Loc.GetString("mothership-pinpointer-disabled")
            : name != null ? Loc.GetString("mothership-pinpointer-tracking", ("name", name))
            : Loc.GetString("mothership-pinpointer-unavailable");
        _off.Disabled = !state.Active;
        _targets.RemoveAllChildren();
        foreach (var (entity, label) in state.Targets)
        {
            var button = new Button { Text = label, Disabled = state.Active && state.Selected == entity };
            button.OnPressed += _ => Selected?.Invoke(entity);
            _targets.AddChild(button);
        }
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.Controls;
using Content.Shared._IS14.Cyborgs;
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
    private readonly Label _status = new();
    private readonly BoxContainer _borgs = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    public event Action<MothershipEyeCommand, NetEntity?>? Command;

    public MothershipEyeWindow()
    {
        Title = Loc.GetString("mothership-eye-title");
        MinWidth = 440;
        MinHeight = 300;
        var contents = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        ContentsContainer.AddChild(contents);
        contents.AddChild(_status);
        var controls = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        contents.AddChild(controls);
        AddButton(controls, "mothership-eye-ship", MothershipEyeCommand.Ship);
        AddButton(controls, "mothership-eye-return", MothershipEyeCommand.Return);
        var scroll = new ScrollContainer { VerticalExpand = true };
        contents.AddChild(scroll);
        scroll.AddChild(_borgs);
    }

    private void AddButton(BoxContainer row, string text, MothershipEyeCommand command, NetEntity? target = null)
    {
        var button = new Button { Text = Loc.GetString(text) };
        button.OnPressed += _ => Command?.Invoke(command, target);
        row.AddChild(button);
    }

    public void UpdateState(MothershipEyeState state)
    {
        _status.Text = state.Following is { } following
            ? Loc.GetString("mothership-eye-following", ("name", following))
            : Loc.GetString(state.Active ? "mothership-eye-free" : "mothership-eye-home");
        _borgs.RemoveAllChildren();
        foreach (var (entity, name) in state.Borgs)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(new Label { Text = name, HorizontalExpand = true });
            AddButton(row, "mothership-eye-jump", MothershipEyeCommand.Jump, entity);
            AddButton(row, "mothership-eye-follow", MothershipEyeCommand.Follow, entity);
            _borgs.AddChild(row);
        }
    }
}

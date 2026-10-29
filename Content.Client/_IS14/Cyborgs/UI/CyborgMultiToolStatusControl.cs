// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._IS14.Cyborgs.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Cyborgs.UI;

/// <summary>
/// Item status of a cycling cyborg tool: the mode it is in, like the upstream multiple tool shows.
/// </summary>
public sealed class CyborgMultiToolStatusControl : Control
{
    private readonly CyborgMultiToolComponent _tool;
    private readonly RichTextLabel _label;
    private int? _shown;

    public CyborgMultiToolStatusControl(CyborgMultiToolComponent tool)
    {
        _tool = tool;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        Update();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Update();
    }

    private void Update()
    {
        if (_shown == _tool.Current || _tool.Current >= _tool.Modes.Count)
            return;

        _shown = _tool.Current;
        _label.SetMarkup(Loc.GetString("cyborg-multitool-mode",
            ("mode", Loc.GetString(_tool.Modes[_tool.Current].Name))));
    }
}

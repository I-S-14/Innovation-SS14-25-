// SPDX-License-Identifier: MIT

using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Robust.Client.Replays.Commands;
using Robust.Client.Replays.UI;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.Replay;

public sealed partial class ReplayToggleScreenshotModeCommand : BaseReplayCommand
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private ContentReplayPlaybackManager _replayManager = default!;

    public override string Command => "replay_toggle_screenshot_mode";
    // IS14: keep the content-specific behavior description separate from engine localization.
    public override string Description => Loc.GetString("cmd-is14-replay-toggle-screenshot-mode-desc");
    public override string Help => Loc.GetString("cmd-is14-replay-toggle-screenshot-mode-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var screen = _userInterfaceManager.ActiveScreen;
        if (screen == null)
            return;

        _replayManager.IsScreenshotMode = !_replayManager.IsScreenshotMode;

        var showReplayWidget = _replayManager.IsScreenshotMode;
        screen.ShowWidget<ReplayControlWidget>(showReplayWidget);

        foreach (var chatBox in _userInterfaceManager.GetUIController<ChatUIController>().Chats)
        {
            chatBox.ChatInput.Visible = !showReplayWidget;
            if (!showReplayWidget)
                chatBox.ChatInput.ChannelSelector.Select(ChatSelectChannel.Local);
        }
    }
}

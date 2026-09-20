using Content.Client._IS14.Economy.Gosplan;
using Content.Shared._IS14.Economy.Gosplan;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.OS.Apps;

/// <summary>
///     The plan board inside the OS: the standalone board's own panel, minus its window frame
///     (Docs §12.2). Read-only, so there is nothing to send back.
/// </summary>
public sealed class GosplanAppUi : IS14OsAppUi
{
    private GosplanConsolePanel? _panel;

    private GosplanConsolePanel Panel => _panel ??= new GosplanConsolePanel();

    public override string AppId => "AppGosplan";

    public override Control Root => Panel;

    public override void UpdateState(IS14OsAppState state)
    {
        if (state is OsConsoleState { State: GosplanConsoleUiState gosplan })
            Panel.UpdateState(gosplan);
    }
}

using Content.Client._IS14.Economy.Treasury;
using Content.Shared._IS14.Economy.Treasury;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.OS.Apps;

/// <summary>
///     The treasury inside the OS: the standalone console's own panel, minus its window frame
///     (Docs §12.2). Like the payroll app it keeps the console's palette rather than the device
///     theme, so a head recognises the same screen in either place.
/// </summary>
public sealed class TreasuryAppUi : IS14OsAppUi
{
    private TreasuryConsolePanel? _panel;

    private TreasuryConsolePanel Panel => _panel ??= new TreasuryConsolePanel();

    public override string AppId => "AppTreasury";

    public override Control Root => Panel;

    public override void Setup(IS14OsBui bui)
    {
        base.Setup(bui);

        Panel.OnTransfer += (target, amount) =>
            SendAppEvent(AppId, new OsConsoleMessageEvent(new TreasuryTransferMessage(target, amount)));
    }

    public override void UpdateState(IS14OsAppState state)
    {
        if (state is OsConsoleState { State: TreasuryConsoleUiState treasury })
            Panel.UpdateState(treasury);
    }
}

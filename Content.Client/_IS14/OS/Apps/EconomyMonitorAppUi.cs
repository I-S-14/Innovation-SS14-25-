using Content.Client._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.OS.Apps;

/// <summary>
///     The economy monitor inside the OS: the standalone console's own panel, minus its window
///     frame (Docs §12.2). It is the widest app on the platform, which is precisely why it
///     belongs on a head's console rather than in a pocket.
/// </summary>
public sealed class EconomyMonitorAppUi : IS14OsAppUi
{
    private EconomyMonitorPanel? _panel;

    private EconomyMonitorPanel Panel => _panel ??= new EconomyMonitorPanel();

    public override string AppId => "AppEconomyMonitor";

    public override Control Root => Panel;

    public override void Setup(IS14OsBui bui)
    {
        base.Setup(bui);

        Panel.OnDeleteRecords += ids =>
            SendAppEvent(AppId, new OsConsoleMessageEvent(new EconomyMonitorDeleteMessage(ids)));

        Panel.OnPrintRecords += ids =>
            SendAppEvent(AppId, new OsConsoleMessageEvent(new EconomyMonitorPrintMessage(ids)));
    }

    public override void UpdateState(IS14OsAppState state)
    {
        if (state is OsConsoleState { State: EconomyMonitorUiState monitor })
            Panel.UpdateState(monitor);
    }
}

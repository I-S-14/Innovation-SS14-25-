using Content.Shared._IS14.Economy.EconomyMonitor;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.EconomyMonitor;

public sealed class EconomyMonitorBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private EconomyMonitorWindow? _window;

    public EconomyMonitorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<EconomyMonitorWindow>();
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is EconomyMonitorUiState s)
            _window?.UpdateState(s);
    }
}

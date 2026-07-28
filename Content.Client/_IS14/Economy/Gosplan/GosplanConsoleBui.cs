using Content.Shared._IS14.Economy.Gosplan;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.Gosplan;

public sealed class GosplanConsoleBui : BoundUserInterface
{
    [ViewVariables]
    private GosplanConsoleWindow? _window;

    public GosplanConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<GosplanConsoleWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is GosplanConsoleUiState s)
            _window?.UpdateState(s);
    }
}

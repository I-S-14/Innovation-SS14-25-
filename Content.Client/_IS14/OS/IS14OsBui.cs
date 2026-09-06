using Content.Client._IS14.OS.Shell;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared._IS14.OS.UI;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._IS14.OS;

/// <summary>
///     BUI for any device running IS14 OS.
///     Named *Bui on purpose: the engine resolves client BUI types by FullName suffix match
///     without a dot boundary, so a name ending in an upstream BUI class name would hijack it.
/// </summary>
public sealed class IS14OsBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    [ViewVariables]
    private IS14OsShellWindow? _window;

    public IS14OsBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<IS14OsShellWindow>();
        _window.Setup(this, _proto);

        _window.OnOpenApp += app => SendMessage(new IS14OsShellMessage(OsShellAction.OpenApp, app));
        _window.OnCloseApp += app => SendMessage(new IS14OsShellMessage(OsShellAction.CloseApp, app));
        _window.OnMinimizeApp += app => SendMessage(new IS14OsShellMessage(OsShellAction.MinimizeApp, app));
        _window.OnFocusApp += app => SendMessage(new IS14OsShellMessage(OsShellAction.FocusApp, app));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IS14OsUiState osState)
            _window?.UpdateState(osState);
    }

    public void SendAppEvent(ProtoId<IS14OsAppPrototype> app, IS14OsAppEvent ev)
    {
        SendMessage(new IS14OsAppMessage(app, ev));
    }

    public void SendUninstall(ProtoId<IS14OsAppPrototype> app)
    {
        SendMessage(new IS14OsShellMessage(OsShellAction.UninstallApp, app));
    }

    public void SendSetTheme(string themeId)
    {
        SendMessage(new IS14OsShellMessage(OsShellAction.SetTheme, null, themeId));
    }

    public void SendToggleFlashlight()
    {
        SendMessage(new IS14OsShellMessage(OsShellAction.ToggleFlashlight));
    }

    public void SendShellAction(OsShellAction action)
    {
        SendMessage(new IS14OsShellMessage(action));
    }
}

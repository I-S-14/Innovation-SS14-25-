using Content.Client._IS14.Controls;
using Content.Shared._IS14.OS.UI;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.OS;

/// <summary>
///     Client half of an application. One instance lives per open window.
///     Unlike upstream UI fragments this is not tied to an entity — apps are prototypes,
///     so the shell builds the UI straight from the app id.
/// </summary>
public abstract class IS14OsAppUi
{
    protected IS14OsBui Bui = default!;

    /// <summary>The osApp prototype this UI belongs to.</summary>
    public abstract string AppId { get; }

    /// <summary>
    ///     The control the shell puts inside the app window. Implementations build it lazily:
    ///     the registry instantiates every app UI once just to read <see cref="AppId"/>, and
    ///     those throwaway instances must not construct control trees.
    /// </summary>
    public abstract Control Root { get; }

    public virtual void Setup(IS14OsBui bui)
    {
        Bui = bui;
    }

    /// <summary>Shell-wide data (owner, station, memory). Called on every state push.</summary>
    public virtual void UpdateShell(OsShellState shell)
    {
    }

    /// <summary>App-specific state. Only called when the server actually sent one.</summary>
    public virtual void UpdateState(IS14OsAppState state)
    {
    }

    public virtual void ApplyTheme(IS14ThemePalette palette)
    {
    }

    protected void SendAppEvent(string appId, IS14OsAppEvent ev)
    {
        Bui.SendAppEvent(appId, ev);
    }
}

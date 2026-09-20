using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     Carries an existing console's own BUI state into the OS unchanged (Docs §12.2).
///
///     The economy consoles were already split into logic and a thin UI wrapper, so moving
///     them onto the platform is a change of envelope, not of contents: the same state class
///     the standalone console sends is the one the app receives, and the same window layout
///     draws it. That is what makes the migration reversible — the old consoles keep working
///     off the very same code the whole time.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsConsoleState : IS14OsAppState
{
    public BoundUserInterfaceState? State;

    public OsConsoleState(BoundUserInterfaceState? state)
    {
        State = state;
    }
}

/// <summary>
///     The other direction: one of the console's own BUI messages, sent through the OS app
///     channel. The server re-stamps <c>Actor</c> from the OS envelope before handing it on,
///     so a console operation still knows who pressed the button.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsConsoleMessageEvent : IS14OsAppEvent
{
    public BoundUserInterfaceMessage Message;

    public OsConsoleMessageEvent(BoundUserInterfaceMessage message)
    {
        Message = message;
    }
}

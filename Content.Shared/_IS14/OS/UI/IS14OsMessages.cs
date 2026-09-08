using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI;

[Serializable, NetSerializable]
public sealed class IS14OsShellMessage : BoundUserInterfaceMessage
{
    public OsShellAction Action;
    public ProtoId<IS14OsAppPrototype>? App;
    public string? Arg;

    public IS14OsShellMessage(OsShellAction action, ProtoId<IS14OsAppPrototype>? app = null, string? arg = null)
    {
        Action = action;
        App = app;
        Arg = arg;
    }
}

[Serializable, NetSerializable]
public enum OsShellAction : byte
{
    OpenApp,
    CloseApp,
    MinimizeApp,
    FocusApp,
    UninstallApp,
    SetTheme,
    ToggleFlashlight,
    ShowRingtone,
    ShowUplink,
    LockUplink,
    CloseLid,
}

/// <summary>Envelope for app traffic. The server verifies the app is installed and open first.</summary>
[Serializable, NetSerializable]
public sealed class IS14OsAppMessage : BoundUserInterfaceMessage
{
    public ProtoId<IS14OsAppPrototype> App;
    public IS14OsAppEvent Event;

    public IS14OsAppMessage(ProtoId<IS14OsAppPrototype> app, IS14OsAppEvent ev)
    {
        App = app;
        Event = ev;
    }
}

[Serializable, NetSerializable]
public abstract class IS14OsAppEvent
{
}

/// <summary>
///     Raised on the device once an app message passed validation. App systems subscribe to it
///     against their own data component and check the concrete event type.
/// </summary>
[ByRefEvent]
public record struct OsAppEventRaised(ProtoId<IS14OsAppPrototype> App, IS14OsAppEvent Event, EntityUid Actor);

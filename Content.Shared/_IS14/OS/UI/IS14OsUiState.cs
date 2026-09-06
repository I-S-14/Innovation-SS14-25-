using System.Numerics;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI;

[Serializable, NetSerializable]
public enum IS14OsUiKey : byte
{
    Key,
}

/// <summary>
///     Composite state: the shell plus one entry per open window. A window whose
///     <see cref="OsWindowState.AppState"/> is null did not change and the client keeps
///     whatever it already had. See Docs/_IS14/os-design.md §4.5.
/// </summary>
[Serializable, NetSerializable]
public sealed class IS14OsUiState : BoundUserInterfaceState
{
    public OsShellState Shell;
    public List<OsWindowState> Windows;

    public IS14OsUiState(OsShellState shell, List<OsWindowState> windows)
    {
        Shell = shell;
        Windows = windows;
    }
}

[Serializable, NetSerializable]
public sealed class OsShellState
{
    public bool Powered;
    public bool Booting;
    public TimeSpan BootStart;
    public TimeSpan BootEnd;

    public string ProfileId = string.Empty;
    public string ThemeId = string.Empty;
    public OsShellMode ShellMode;
    public int MaxWindows;
    public Vector2 ScreenSize;

    public int MemoryTotal;
    public int MemoryUsed;
    public int MemorySystem;
    public int MemorySlotsFree;

    /// <summary>Charge 0..1, or null when the device does not run on a cell at all.</summary>
    public float? Battery;

    public List<ProtoId<IS14OsAppPrototype>> Installed = new();
    public List<ProtoId<IS14OsAppPrototype>> Open = new();
    public List<ProtoId<IS14OsAppPrototype>> Minimized = new();

    /// <summary>Themes this device is allowed to switch to right now.</summary>
    public List<ProtoId<IS14OsThemePrototype>> Themes = new();

    // Tray / status readouts. Kept in the shell rather than in an app state because the
    // taskbar shows them too.
    public string DeviceName = string.Empty;
    public string? OwnerName;
    public string? IdName;
    public string? IdJob;
    public string? StationName;
    public string? AlertLevel;
    public Color AlertColor = Color.White;
    public string? AlertInstructions;
    public string? Address;
    public bool FlashlightOn;
    public bool HasFlashlight;
    public bool HasRinger;
    public bool HasUplink;
}

[Serializable, NetSerializable]
public sealed class OsWindowState
{
    public ProtoId<IS14OsAppPrototype> App;
    public bool Minimized;
    public IS14OsAppState? AppState;

    public OsWindowState(ProtoId<IS14OsAppPrototype> app, bool minimized, IS14OsAppState? appState)
    {
        App = app;
        Minimized = minimized;
        AppState = appState;
    }
}

/// <summary>Base for per-app state. Apps subclass this; the shell only routes it.</summary>
[Serializable, NetSerializable]
public abstract class IS14OsAppState
{
}

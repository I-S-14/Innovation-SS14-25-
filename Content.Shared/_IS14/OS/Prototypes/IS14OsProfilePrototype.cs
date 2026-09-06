using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.Prototypes;

/// <summary>
///     A hardware model: what the OS is allowed to do on this particular device.
///     See Docs/_IS14/os-design.md §4.2.
/// </summary>
[Prototype("osProfile")]
public sealed partial class IS14OsProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public OsFormFactor FormFactor = OsFormFactor.Handheld;

    /// <summary>
    ///     How many app windows may be open at once. Handhelds are locked to one: the shell
    ///     then behaves as a fullscreen phone with a task switcher.
    /// </summary>
    [DataField]
    public int MaxWindows = 1;

    /// <summary>
    ///     Total memory in GQ. The platform's main constraint — you cannot install everything.
    /// </summary>
    [DataField]
    public int Memory = 64;

    /// <summary>
    ///     Memory permanently taken by the system apps. Shown as used, never freeable.
    /// </summary>
    [DataField]
    public int SystemMemory = 8;

    [DataField]
    public OsShellMode ShellMode = OsShellMode.Fullscreen;

    /// <summary>
    ///     Logical desktop size. The BUI window is sized from this.
    /// </summary>
    [DataField]
    public Vector2 ScreenSize = new(400, 320);

    /// <summary>
    ///     How many memory expansion boards fit. Not used yet — phase 2.
    /// </summary>
    [DataField]
    public int MemorySlots = 1;

    /// <summary>
    ///     Cold boot duration. A device woken within <see cref="SleepGrace"/> of shutting down
    ///     skips this, so opening and closing the lid repeatedly does not punish the player.
    /// </summary>
    [DataField]
    public TimeSpan BootTime = TimeSpan.FromSeconds(1.6);

    [DataField]
    public TimeSpan SleepGrace = TimeSpan.FromSeconds(30);
}

[Serializable, NetSerializable]
public enum OsFormFactor : byte
{
    Handheld,
    Portable,
    Stationary,
}

[Serializable, NetSerializable]
public enum OsShellMode : byte
{
    /// <summary>One app fills the desktop; the taskbar switches between them.</summary>
    Fullscreen,

    /// <summary>Draggable, resizable, overlapping windows.</summary>
    Windowed,
}

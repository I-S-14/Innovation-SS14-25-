using System.Numerics;
using Content.Shared.Access;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.OS.Prototypes;

/// <summary>
///     An application is not an entity. It is this prototype plus the components in
///     <see cref="Components"/>, which are added to the device itself on install and removed
///     on uninstall — so app data lives and dies with the installation and costs no entities.
///     See Docs/_IS14/os-design.md §6.1.
/// </summary>
[Prototype("osApp")]
public sealed partial class IS14OsAppPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField]
    public SpriteSpecifier? Icon;

    [DataField]
    public OsAppCategory Category = OsAppCategory.Work;

    /// <summary>
    ///     Components added to the device when this app is installed. This is where app state lives.
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();

    /// <summary>Memory in GQ taken by the app itself.</summary>
    [DataField]
    public int Size = 4;

    /// <summary>Extra memory the app's own data may grow into. Phase 2.</summary>
    [DataField]
    public int DataCap;

    /// <summary>Reserved: prices are deliberately not in use yet, memory is the limiter (§7.1).</summary>
    [DataField]
    public int Price;

    [DataField]
    public OsAppSource Source = OsAppSource.NtStore;

    [DataField]
    public OsDeviceFlags DeviceFlags = OsDeviceFlags.All;

    /// <summary>Access needed to run the app, checked against the ID card in the device.</summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> RequiredAccess = new();

    /// <summary>Access needed to download it. Falls back to <see cref="RequiredAccess"/>.</summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> DownloadAccess = new();

    /// <summary>Watts drawn while open. Minimised apps cost half (Docs §5.6).</summary>
    [DataField]
    public float PowerDraw = 0.03f;

    /// <summary>Needs the station network. Not enforced yet — phase 3.</summary>
    [DataField]
    public bool RequiresNetwork;

    /// <summary>Runs entirely on the client and never asks the server for state (Docs §4.5).</summary>
    [DataField]
    public bool ClientOnly;

    /// <summary>
    ///     Worth keeping alive when minimised — a messenger that gets closed to make room stops
    ///     being a messenger. The shell evicts these last.
    /// </summary>
    [DataField]
    public bool Background;

    /// <summary>System apps: cannot be uninstalled and are always installed.</summary>
    [DataField]
    public bool Undeletable;

    /// <summary>Pinned apps sit in the taskbar even when closed.</summary>
    [DataField]
    public bool Pinned;

    /// <summary>Order in the start menu and taskbar. Lower first.</summary>
    [DataField]
    public int Order;

    /// <summary>Default window size in windowed mode.</summary>
    [DataField]
    public Vector2 WindowSize = new(360, 260);
}

[Serializable, NetSerializable]
public enum OsAppCategory : byte
{
    System,
    Work,
    Records,
    Engineering,
    Medical,
    Security,
    Supply,
    Science,
    Service,
    Finance,
    Games,
    Illegal,
}

[Flags]
[Serializable, NetSerializable]
public enum OsAppSource : byte
{
    None = 0,
    Preinstalled = 1 << 0,
    NtStore = 1 << 1,
    Syndinet = 1 << 2,
    Disk = 1 << 3,
    Maints = 1 << 4,
}

[Flags]
[Serializable, NetSerializable]
public enum OsDeviceFlags : byte
{
    None = 0,
    Handheld = 1 << 0,
    Portable = 1 << 1,
    Stationary = 1 << 2,
    All = Handheld | Portable | Stationary,
}

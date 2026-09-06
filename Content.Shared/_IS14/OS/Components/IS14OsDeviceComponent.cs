using Content.Shared._IS14.OS.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.Components;

/// <summary>
///     Marks an entity as running IS14 OS. Server authoritative: everything the client needs
///     travels in the BUI state, everything other players need travels through appearance data.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsDeviceComponent : Component
{
    [DataField(required: true)]
    public ProtoId<IS14OsProfilePrototype> Profile;

    [DataField]
    public ProtoId<IS14OsThemePrototype> Theme = "OsNtOs";

    /// <summary>
    ///     Whether the lid is open. On handhelds the lid is the session: opening it powers the
    ///     OS on and shows the screen, closing it shuts everything down (§5.6).
    /// </summary>
    [DataField]
    public bool LidOpen;

    /// <summary>Stationary devices have no lid and are always considered open.</summary>
    [DataField]
    public bool Lidless;

    [ViewVariables]
    public bool Powered;

    /// <summary>When the current cold boot finishes. Null when not booting.</summary>
    [ViewVariables]
    public TimeSpan? BootEnd;

    /// <summary>Used to skip the boot animation when the lid is flicked shut and open again.</summary>
    [ViewVariables]
    public TimeSpan LastShutdown;

    /// <summary>
    ///     Opened automatically when the device boots. An empty desktop is a dead first
    ///     impression; the status screen is what a player wants nine times out of ten.
    /// </summary>
    [DataField]
    public ProtoId<IS14OsAppPrototype>? DefaultApp = "AppStatus";

    /// <summary>Open apps, last entry is focused / on top.</summary>
    [ViewVariables]
    public List<ProtoId<IS14OsAppPrototype>> Open = new();

    [ViewVariables]
    public HashSet<ProtoId<IS14OsAppPrototype>> Minimized = new();

    /// <summary>RSI state shown when the lid is open. IS14 PDA art already ships these.</summary>
    [DataField]
    public string OpenState = "base";

    [DataField]
    public string ClosedState = "closed";

    [DataField]
    public SoundSpecifier? BootSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");

    [DataField]
    public SoundSpecifier? LidSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
}

[Serializable, NetSerializable]
public enum IS14OsVisuals : byte
{
    LidOpen,
    ScreenOn,
}

public enum IS14OsVisualLayers : byte
{
    Base,
    Screen,
}

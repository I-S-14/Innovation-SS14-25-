using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI;

/// <summary>
///     Link quality between a device and the station network (Docs/_IS14/os-design.md §9).
///     We do not invent an NTNet: the signal is read off the station's telecomms, so cutting
///     comms cuts the OS too, and repairing them brings it back.
/// </summary>
[Serializable, NetSerializable]
public enum OsSignal : byte
{
    /// <summary>No link. Networked apps refuse to run and downloads cannot start.</summary>
    None,

    /// <summary>Off the station grid but still in reach — everything works, slowly.</summary>
    Low,

    /// <summary>On the station, telecomms up.</summary>
    Good,

    /// <summary>Plugged into the station by cable. Stationary consoles never lose signal.</summary>
    Wired,
}

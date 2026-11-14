// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     Cyborg self-diagnosis: the chassis the tablet is built into, read on the server.
///     Null fields are things the cyborg doesn't have, like a cell or an AI.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsCyborgDiagnosticsState : IS14OsAppState
{
    public bool IsCyborg;
    public string? Model;
    public float? Charge;

    /// <summary>Chassis integrity from 0 to 1, 0 being critical.</summary>
    public float Integrity = 1f;

    public int Slots;
    public int BrokenSlots;
    public string? Ai;
    public bool LawSync;
    public bool Lockdown;
    public bool Emagged;
    public List<string> Laws = new();
    public List<string> Upgrades = new();
}

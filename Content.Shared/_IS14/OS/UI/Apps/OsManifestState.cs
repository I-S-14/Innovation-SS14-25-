using Content.Shared.CrewManifest;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     Crew manifest. Reuses the upstream manifest payload rather than copying it into our own
///     shape — the data is identical and the engine already knows how to send it.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsManifestState : IS14OsAppState
{
    public string StationName = string.Empty;
    public CrewManifestEntries? Entries;
}

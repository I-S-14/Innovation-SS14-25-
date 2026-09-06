using Content.Server.CrewManifest;
using Content.Server.Station.Systems;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Crew manifest, read straight off the owning station. Read-only, so it needs no data of
///     its own — the marker component exists only so this system has something to hook onto.
/// </summary>
public sealed class IS14OsManifestSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CrewManifestSystem _manifest = default!;

    public const string AppId = "AppManifest";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsManifestComponent, OsAppGetStateEvent>(OnGetState);
    }

    private void OnGetState(Entity<IS14OsManifestComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var state = new OsManifestState();

        if (_station.GetOwningStation(ent) is { } station)
        {
            var (name, entries) = _manifest.GetCrewManifest(station);
            state.StationName = name;
            state.Entries = entries;
        }

        args.State = state;
    }
}

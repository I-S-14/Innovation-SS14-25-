using Content.Shared.Station.Systems;
// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Audio;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Content.Server.Station.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Audio;

/// <inheritdoc/>
public sealed partial class ServerGlobalSoundSystem : GlobalSoundSystem
{
    //IS14-change start: retain fork fields lost during merge.

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    //IS14-change end

    [Dependency] private IConsoleHost _conHost = default!;

    /// <inheritdoc/>
    public override void Shutdown()
    {
        base.Shutdown();
        _conHost.UnregisterCommand("playglobalsound");
    }

    /// <summary>
    /// Plays an admin sound for the selected players and optionally records it for replay.
    /// </summary>
    /// <param name="playerFilter">Players who should receive the sound.</param>
    /// <param name="specifier">Resolved sound to play.</param>
    /// <param name="audioParams">Optional parameters controlling playback.</param>
    /// <param name="replay">Whether the network event should be recorded for replay.</param>
    [PublicAPI]
    public void PlayAdminGlobal(Filter playerFilter, ResolvedSoundSpecifier specifier, AudioParams? audioParams = null, bool replay = true)
    {
        var msg = new AdminSoundEvent(specifier, audioParams);
        RaiseNetworkEvent(msg, playerFilter, recordReplay: replay);
    }

    private Filter GetStationAndPvs(EntityUid source)
    {
        var stationFilter = _stationSystem.GetInOwningStation(source);
        stationFilter.AddPlayersByPvs(source, entityManager: EntityManager);
        return stationFilter;
    }

    public new void PlayGlobalOnStation(EntityUid source, ResolvedSoundSpecifier specifier, AudioParams? audioParams = null)
    {
        var msg = new GameGlobalSoundEvent(specifier, audioParams);
        var filter = GetStationAndPvs(source);
        RaiseNetworkEvent(msg, filter);
    }

    public new void StopStationEventMusic(EntityUid source, StationEventMusicType type)
    {
        // TODO REPLAYS
        // these start & stop events are gonna be a PITA
        // theres probably some nice way of handling them. Maybe it just needs dedicated replay data (in which case these events should NOT get recorded).

        var msg = new StopStationEventMusic(type);
        var filter = GetStationAndPvs(source);
        RaiseNetworkEvent(msg, filter);
    }

    public void DispatchStationEventMusic(EntityUid source, SoundSpecifier sound, StationEventMusicType type, AudioParams? audioParams = null) // goob edit
    {
        DispatchStationEventMusic(source, _audio.ResolveSound(sound), type, audioParams); // goob edit
    }

    public void DispatchStationEventMusic(EntityUid source, ResolvedSoundSpecifier specifier, StationEventMusicType type, AudioParams? audioParams = null) // goob edit
    {
        var audio = audioParams ?? AudioParams.Default.WithVolume(-8); // goob edit
        var msg = new StationEventMusicEvent(specifier, type, audio);

        var filter = GetStationAndPvs(source);
        RaiseNetworkEvent(msg, filter);
    }
}

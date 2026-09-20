using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared._IS14.OS.UI;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS;

/// <summary>
///     Link quality (Docs/_IS14/os-design.md §9).
///
///     There is no NTNet of our own: the signal is the station's telecomms, the same servers
///     the radio runs on. That is the whole point — cutting comms is already a recognised act
///     of sabotage, and now it takes the messenger, the manifest and the app store with it.
/// </summary>
public sealed class IS14OsNetworkSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;
    [Dependency] private readonly IS14OsMemorySystem _memory = default!;

    /// <summary>
    ///     Telecomms do not flicker on a frame timescale, and every device would otherwise walk
    ///     the whole server list. One sweep a second is far more than the fiction needs.
    /// </summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextTick;

    /// <summary>Per-sweep memo, so twenty PDAs on one station cost one server query.</summary>
    private readonly Dictionary<EntityUid, bool> _stationServers = new();

    /// <summary>Maps a device to the signal it had last sweep, to notice the moment it drops.</summary>
    private readonly Dictionary<EntityUid, OsSignal> _last = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsDeviceComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<IS14OsDeviceComponent> ent, ref ComponentShutdown args)
    {
        _last.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        _stationServers.Clear();

        var query = EntityQueryEnumerator<IS14OsDeviceComponent>();
        while (query.MoveNext(out var uid, out var device))
        {
            var signal = GetSignal(uid, device);

            if (!_last.TryGetValue(uid, out var previous) || previous != signal)
            {
                _last[uid] = signal;
                _os.MarkDirty(uid);
            }

            if (signal == OsSignal.None && device.Powered)
                DropNetworkedApps((uid, device));
        }
    }

    /// <summary>
    ///     An app that needs the network and has lost it is not a window with stale data in it,
    ///     it is a window that lies. It closes, loudly, so the player knows comms are down
    ///     rather than wondering why nobody answers.
    /// </summary>
    private void DropNetworkedApps(Entity<IS14OsDeviceComponent> ent)
    {
        List<ProtoId<IS14OsAppPrototype>>? doomed = null;

        foreach (var app in ent.Comp.Open)
        {
            if (_proto.TryIndex(app, out var proto) && proto.RequiresNetwork)
                (doomed ??= new List<ProtoId<IS14OsAppPrototype>>()).Add(app);
        }

        if (doomed == null)
            return;

        foreach (var app in doomed)
        {
            _os.CloseApp(ent, app);
        }

        _popup.PopupEntity(Loc.GetString("is14-os-network-lost"), ent);
        _os.UpdateUi(ent.Owner, ent.Comp);
    }

    /// <summary>
    ///     Signal for one device. Cheap enough to call from the state build; the station lookup
    ///     is the only part that is not a component check.
    /// </summary>
    public OsSignal GetSignal(EntityUid uid, IS14OsDeviceComponent? device = null)
    {
        if (!Resolve(uid, ref device, false) || !device.Powered)
            return OsSignal.None;

        // A console bolted to the station is wired into it. Losing that link means losing the
        // console's own power, at which point the signal indicator is not what you are reading.
        if (_memory.GetProfile(device)?.FormFactor == OsFormFactor.Stationary)
            return OsSignal.Wired;

        // A device in a bag in a locker can lose its own grid; whoever is carrying it has one.
        var station = _station.GetOwningStation(uid)
            ?? _station.GetOwningStation(_os.FindCarrier(uid));

        if (station != null && HasActiveServer(station.Value))
            return OsSignal.Good;

        // Off the station but still within earshot of it: a spacewalk, a shuttle alongside.
        return HasServerOnMap(Transform(uid).MapID) ? OsSignal.Low : OsSignal.None;
    }

    /// <summary>Fraction of nominal download speed. Weak signal is slow, not broken (§7.1).</summary>
    public float GetSpeedMultiplier(OsSignal signal)
    {
        return signal switch
        {
            OsSignal.Wired => 1.5f,
            OsSignal.Good => 1f,
            OsSignal.Low => 0.35f,
            _ => 0f,
        };
    }

    private bool HasActiveServer(EntityUid station)
    {
        if (_stationServers.TryGetValue(station, out var cached))
            return cached;

        var result = false;
        var query = EntityQueryEnumerator<TelecomServerComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out _, out var power))
        {
            if (!power.Powered || _station.GetOwningStation(uid) != station)
                continue;

            result = true;
            break;
        }

        _stationServers[station] = result;
        return result;
    }

    private bool HasServerOnMap(MapId map)
    {
        var query = EntityQueryEnumerator<TelecomServerComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var power, out var xform))
        {
            if (power.Powered && xform.MapID == map)
                return true;
        }

        return false;
    }
}

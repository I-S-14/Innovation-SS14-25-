using Content.Server.Station.Systems;
using Content.Shared.Station.Systems; // IS14-change: upstream API move
using Robust.Shared.Configuration;
using CVars = Content.Shared._CorvaxGoob.CCCVars.CCCVars;

namespace Content.Server._CorvaxGoob.Jobs;

public sealed class MrpJobServerSystem : EntitySystem
{
    [Dependency] private readonly ServerStationJobsSystem _stationJobs = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized, after: new[] { typeof(ServerStationJobsSystem) });
    }

    private void OnStationInitialized(StationInitializedEvent ev)
    {
        var mrpEnabled = _cfg.GetCVar(CVars.MrpJobsEnabled);
        _stationJobs.ApplyMrpJobsFilter(ev.Station, mrpEnabled);
    }
}

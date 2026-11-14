using Robust.Shared.Prototypes;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;

namespace Content.IntegrationTests.Tests._IS14;

[Category("MapTests")]
public sealed class StationMapCompatibilityTest : GameTest
{
    private readonly ProtoId<GameMapPrototype> CompatibilityAtlasGameMapId = "Atlas";

    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task AtlasInitializesWithRetainedIs14Jobs()
    {
        await Server.WaitAssertion(() =>
        {
            var prototype = Server.ProtoMan.Index<GameMapPrototype>(CompatibilityAtlasGameMapId);
            var grids = Server.System<ServerGameTicker>().LoadGameMap(prototype, out _,
                DeserializationOptions.Default with { InitializeMaps = true });
            Assert.That(grids, Is.Not.Empty);
            var station = Server.System<ServerStationSystem>().GetOwningStation(grids[0]);
            Assert.That(station, Is.Not.Null);
            var jobs = Server.System<ServerStationJobsSystem>().GetJobs(station!.Value);
            foreach (var job in new[] { "JobHosMilitia", "JobMilitia", "IS_JobCadet", "JobMupRepresentative" })
                Assert.That(jobs.ContainsKey(job), Is.True, job);
            Assert.That(jobs.ContainsKey("SecurityOfficer"), Is.False);
        });
        // Surface errors from deferred node/network setup as well as map deserialization.
        await Server.WaitRunTicks(2);
    }
}

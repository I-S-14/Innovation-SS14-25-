using Content.Shared.Weather;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyAshfallSchedulerTest
{
    private readonly EntProtoId CompatibilityTestIS14AshfallScheduleEntityId = "TestIS14AshfallSchedule";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestIS14AshfallSchedule
  components:
  - type: WeatherScheduler
    stages:
    - weather: AshfallLight
      duration: {min: 60, max: 60}
    - weather: Ashfall
      duration: {min: 60, max: 60}
";

    [Test]
    public async Task GoobSchedulerStartsLightAshAndTransitionsToTheWizardsAshStatusEffect()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitPost(() =>
        {
            var prototype = server.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>(CompatibilityTestIS14AshfallScheduleEntityId);
            server.EntMan.AddComponents(map.MapUid, prototype, removeExisting: false);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<SharedWeatherSystem>().HasWeather(map.MapId, "WeatherAshfallLight"), Is.True);
            Assert.That(server.System<SharedWeatherSystem>().HasWeather(map.MapId, "WeatherAshfall"), Is.False);
        });
        await pair.RunSeconds(61);
        await server.WaitAssertion(() =>
            Assert.That(server.System<SharedWeatherSystem>().HasWeather(map.MapId, "WeatherAshfall"), Is.True));
        await pair.CleanReturnAsync();
    }
}

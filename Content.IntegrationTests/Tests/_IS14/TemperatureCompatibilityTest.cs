using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class TemperatureCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestNewTemperature
  components:
  - type: Temperature
    temperature: 300
    heatCapacity: 100
    thermalConductivity: 5
- type: entity
  id: IS14TestOldTemperature
  components:
  - type: Temperature
    currentTemperature: 310
";

    [TestCase("IS14TestNewTemperature", 300f)]
    [TestCase("IS14TestOldTemperature", 310f)]
    public async Task BothTemperatureNamesUseTheSameState(string prototype, float initial)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entity = pair.Server.EntMan.SpawnEntity(prototype, Robust.Shared.Map.MapCoordinates.Nullspace);
            var temperature = pair.Server.EntMan.GetComponent<TemperatureComponent>(entity);
            Assert.That(temperature.CurrentTemperature, Is.EqualTo(initial));
            Assert.That(temperature.Temperature, Is.EqualTo(initial));
            temperature.Temperature = 280;
            Assert.That(temperature.CurrentTemperature, Is.EqualTo(280));
            temperature.CurrentTemperature = 320;
            Assert.That(temperature.Temperature, Is.EqualTo(320));
            pair.Server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(1f)]
    [TestCase(10000f)]
    public async Task ConductivityTransfersHeatWithoutOvershootAndConservesEnergy(float seconds)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entity = server.EntMan.SpawnEntity("IS14TestNewTemperature", map.GridCoords);
            var temperature = server.EntMan.GetComponent<TemperatureComponent>(entity);
            var gas = new GasMixture(2500) { Temperature = 400 };
            gas.AdjustMoles(Gas.Oxygen, 10);
            var capacity = server.System<SharedAtmosphereSystem>().GetHeatCapacity(gas, false);
            Assert.That(server.System<SharedTemperatureSystem>().GetHeatCapacity(entity), Is.EqualTo(100));
            var energy = 300 * 100 + 400 * capacity;
            var exposure = new AtmosExposedUpdateEvent(map.GridCoords, gas,
                server.EntMan.GetComponent<TransformComponent>(entity), seconds, 2);
            server.EntMan.EventBus.RaiseLocalEvent(entity, ref exposure);
            Assert.That(temperature.CurrentTemperature, Is.InRange(300f, 400f));
            Assert.That(gas.Temperature, Is.GreaterThanOrEqualTo(temperature.CurrentTemperature - 0.001f));
            Assert.That(temperature.CurrentTemperature * 100 + gas.Temperature * capacity,
                Is.EqualTo(energy).Within(0.05f));
            if (seconds == 1)
                Assert.That(temperature.CurrentTemperature, Is.EqualTo(310f).Within(0.001f));
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}

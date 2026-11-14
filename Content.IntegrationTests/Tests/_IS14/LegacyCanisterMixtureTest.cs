using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Components;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyCanisterMixtureTest
{
    [TestCase("AirCanister", Gas.Oxygen, 581.56f, Gas.Nitrogen, 2187.79f, 293.15f)]
    [TestCase("OxygenCanister", Gas.Oxygen, 2769.36f, Gas.Nitrogen, 0f, 293.15f)]
    [TestCase("LiquidOxygenCanister", Gas.Oxygen, 18710.71051f, Gas.Nitrogen, 0f, 72f)]
    [TestCase("NitrogenCanister", Gas.Nitrogen, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("LiquidNitrogenCanister", Gas.Nitrogen, 18710.71051f, Gas.Oxygen, 0f, 72f)]
    [TestCase("CarbonDioxideCanister", Gas.CarbonDioxide, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("LiquidCarbonDioxideCanister", Gas.CarbonDioxide, 18710.71051f, Gas.Oxygen, 0f, 72f)]
    [TestCase("PlasmaCanister", Gas.Plasma, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("TritiumCanister", Gas.Tritium, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("WaterVaporCanister", Gas.WaterVapor, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("AmmoniaCanister", Gas.Ammonia, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("NitrousOxideCanister", Gas.NitrousOxide, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("FrezonCanister", Gas.Frezon, 2769.36f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("OxygenAndNitrousOxideCanister", Gas.Oxygen, 1310.197357f, Gas.NitrousOxide, 561.513153f, 293.15f)]
    [TestCase("BZCanister", Gas.BZ, 1871.71051f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("HealiumCanister", Gas.Healium, 1871.71051f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("NitriumCanister", Gas.Nitrium, 1871.71051f, Gas.Oxygen, 0f, 293.15f)]
    [TestCase("PluoxiumCanister", Gas.Pluoxium, 1871.71051f, Gas.Oxygen, 0f, 293.15f)]
    public async Task SpawnPreservesActualGasMixture(string prototype, Gas primary, float primaryMoles,
        Gas secondary, float secondaryMoles, float temperature)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entity = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var canister = server.EntMan.GetComponent<GasCanisterComponent>(entity);
            Assert.That(canister.Air.Volume, Is.EqualTo(1000f));
            Assert.That(canister.Air.Temperature, Is.EqualTo(temperature).Within(0.001f));
            Assert.That(canister.Air.GetMoles(primary), Is.EqualTo(primaryMoles).Within(0.005f));
            Assert.That(canister.Air.GetMoles(secondary), Is.EqualTo(secondaryMoles).Within(0.005f));
            Assert.That(canister.Air.TotalMoles, Is.EqualTo(primaryMoles + secondaryMoles).Within(0.005f));
            Assert.That(canister.Air.Pressure,
                Is.EqualTo((primaryMoles + secondaryMoles) * Atmospherics.R * temperature / 1000f).Within(0.1f));
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}

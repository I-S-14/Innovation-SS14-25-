using Content.Shared.Nutrition.EntitySystems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ThirstThresholdCompatibilityTest
{
    [TestCase(450, ThirstThreshold.Okay)]
    [TestCase(451, ThirstThreshold.OverHydrated)]
    [TestCase(600, ThirstThreshold.OverHydrated)]
    public async Task LegacyThirstFacadeRecognizesTheModernOverhydratedThreshold(int value, ThirstThreshold expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            var thirst = server.System<ThirstSystem>();
            thirst.SetThirst(human, null, value);
            Assert.That(thirst.GetThirst(human), Is.EqualTo(value));
            Assert.That(thirst.GetThirstThreshold(human), Is.EqualTo(expected));
            server.EntMan.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}

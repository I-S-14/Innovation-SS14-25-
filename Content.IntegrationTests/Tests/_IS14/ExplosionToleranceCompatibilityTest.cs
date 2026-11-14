using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ExplosionToleranceCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: damageModifierSet
  id: IS14TestFlatHeatArmor
  flatReductions:
    Heat: 10
- type: entity
  parent: WallSolid
  id: IS14TestFlatHeatWall
  components:
  - type: Damageable
    damageModifierSet: IS14TestFlatHeatArmor
- type: damageModifierSet
  id: IS14TestImmuneHeatArmor
  coefficients:
    Heat: 0
  flatReductions:
    Heat: 10
- type: entity
  parent: IS14TestFlatHeatWall
  id: IS14TestImmuneHeatWall
  components:
  - type: Damageable
    damageModifierSet: IS14TestImmuneHeatArmor
";

    [TestCase("IS14TestFlatHeatWall")]
    [TestCase("IS14TestImmuneHeatWall")]
    public async Task ZeroDamageDoesNotBreakAirtightInitialization(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            // Anchoring calculates tolerance against every explosion prototype,
            // including Goob's zero-Heat Corpse explosion and this flat heat armor.
            var wall = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.EntMan.GetComponent<TransformComponent>(wall).Anchored, Is.True);
            server.EntMan.DeleteEntity(wall);
        });
        await pair.CleanReturnAsync();
    }
}

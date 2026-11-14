using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DimensionPotHealthTest
{
    [TestCase("PottedPlant1", 40)]
    [TestCase("DimensionPot", 2000000)]
    public async Task PotKeepsItsOwnDestructionThreshold(string prototype, int threshold)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid pot = default;
        await server.WaitAssertion(() =>
        {
            pot = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.System<DamageableSystem>().TryChangeDamage(pot,
                new DamageSpecifier { DamageDict = { ["Blunt"] = threshold - 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(pot), Is.True,
                "The inherited furniture threshold must not destroy the pocket dimension's entrance.");
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(pot), Is.EqualTo(FixedPoint2.New(threshold - 1)));
            server.System<DamageableSystem>().TryChangeDamage(pot,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(pot), Is.False);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}

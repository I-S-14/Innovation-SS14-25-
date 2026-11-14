using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedRobotThresholdTest
{
    [TestCase("GunBot", 80, 1, 1)]
    [TestCase("NukeBot", 140, 2, 3)]
    [TestCase("MobAncientRepairBot", 30, 0, 0)]
    [TestCase("MobAncientGuardBot", 50, 0, 0)]
    [TestCase("MobFireBot", 120, 0, 0)]
    [TestCase("MobGoidaBot", 220, 0, 0)]
    public async Task RobotKeepsItsOwnHealthAndLoot(string prototype, int threshold, int minOre, int maxOre)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid robot = default;
        await server.WaitAssertion(() =>
        {
            robot = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.System<DamageableSystem>().TryChangeDamage(robot,
                new DamageSpecifier { DamageDict = { ["Blunt"] = threshold - 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(robot), Is.True, "An earlier inherited threshold must not destroy the upgraded robot.");
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(robot), Is.EqualTo(FixedPoint2.New(threshold - 1)));
            Assert.That(OreCount(), Is.Zero);
            server.System<DamageableSystem>().TryChangeDamage(robot,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(robot), Is.False);
            Assert.That(OreCount(), Is.InRange(minOre, maxOre), "The gunbot and syndicate variant have distinct ore drops.");
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();

        int OreCount() => server.EntMan.EntityQuery<StackComponent>()
            .Where(c => c.StackTypeId.Id == "DiamondOreUnprocessed").Sum(c => c.Count);
    }
}

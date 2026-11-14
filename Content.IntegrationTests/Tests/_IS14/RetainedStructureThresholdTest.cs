using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedStructureThresholdTest
{
    [TestCase("RitualCircleInact", 25)]
    [TestCase("RitualCircle1", 30)]
    [TestCase("RitualCircle2", 40)]
    [TestCase("RitualCircle3", 50)]
    [TestCase("RitualCircle4", 75)]
    [TestCase("RitualCircle5", 100)]
    [TestCase("HolosignWetFloor", 30)]
    [TestCase("HolosignSecurityAdvanced", 100)]
    public async Task StructureKeepsItsOwnDestructionThreshold(string prototype, int threshold)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid structure = default;
        await server.WaitAssertion(() =>
        {
            structure = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.System<DamageableSystem>().TryChangeDamage(structure,
                new DamageSpecifier { DamageDict = { ["Blunt"] = threshold - 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(structure), Is.True,
                "An upgraded structure must not inherit an earlier destruction threshold.");
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(structure), Is.EqualTo(FixedPoint2.New(threshold - 1)));
            server.System<DamageableSystem>().TryChangeDamage(structure,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(structure), Is.False);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}

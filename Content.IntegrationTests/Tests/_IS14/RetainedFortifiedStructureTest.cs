#nullable enable annotations
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedFortifiedStructureTest
{
    [TestCase("Candle", 50, null, 0, 0)]
    [TestCase("SpiritCandle", 60, "HeadSkeleton", 1, 1)]
    [TestCase("Window", 80, "ShardGlass", 1, 2)]
    [TestCase("WindowCosmicCult", 150, "ShardGlass", 0, 0)]
    [TestCase("WindowDirectional", 30, "ShardGlass", 1, 1)]
    [TestCase("WindowTintedDirectional", 37, "ShardGlassReinforced", 1, 1)]
    [TestCase("fullWallFlock", 300, null, 0, 0)]
    [TestCase("WallFeatherZoneShuttle", 600, null, 0, 0)]
    public async Task HealthAndBreakLootRemainDistinct(string prototype, int threshold, string? drop, int min, int max)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid target = default;
        await server.WaitAssertion(() =>
        {
            target = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.System<DamageableSystem>().TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { ["Blunt"] = threshold - 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(target), Is.True, "An earlier inherited threshold must not destroy this variant.");
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(target), Is.EqualTo(FixedPoint2.New(threshold - 1)));
            if (drop != null)
                Assert.That(Count(drop), Is.Zero, "Break loot must not appear before this variant's own threshold.");
            server.System<DamageableSystem>().TryChangeDamage(target,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(target), Is.False);
            if (drop != null)
                Assert.That(Count(drop), Is.InRange(min, max));
            if (prototype == "WindowTintedDirectional")
                Assert.That(Count("ShardGlass"), Is.Zero, "The reinforced variant must not also inherit the ordinary glass drop.");
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();

        int Count(string id) => server.EntMan.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == id);
    }
}

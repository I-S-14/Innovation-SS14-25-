using System.Linq;
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedBananiumRockTest
{
    [TestCase("WallSpawnAsteroidBananium", "AsteroidRockBananium", "BananiumOre1Unprocessed")]
    [TestCase("WallSpawnAsteroidBananiumCrab", "AsteroidRockBananiumCrab", "MobBananiumCrab")]
    public async Task AnomalyRockSpawnsAndMiningReleasesItsContents(string effectId, string rockId, string lootId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid effect = default, rock = default;
        await server.WaitAssertion(() =>
        {
            effect = server.EntMan.SpawnEntity(effectId,
                map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(effect), Is.False);
            var rocks = em.EntityQuery<MetaDataComponent>().Where(c => c.EntityPrototype?.ID == rockId).ToArray();
            Assert.That(rocks, Has.Length.EqualTo(1));
            rock = rocks[0].Owner;
            server.System<DamageableSystem>().TryChangeDamage(rock,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1000 } }, true);
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(rock), Is.False);
            var contents = em.EntityQuery<MetaDataComponent>().Where(c => c.EntityPrototype?.ID == lootId).ToArray();
            Assert.That(contents.Length, Is.InRange(1, lootId == "MobBananiumCrab" ? 1 : 3));
            foreach (var content in contents)
                em.DeleteEntity(content.Owner);
        });
        await pair.CleanReturnAsync();
    }
}

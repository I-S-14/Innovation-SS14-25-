using Content.Goobstation.Common.Religion;
using Content.Shared.Bible;
using Content.Shared.Bible.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class BibleSummoningCompatibilityTest
{
    [TestCase("Bible", "SpawnPointGhostRemilia")]
    [TestCase("BibleNecronomicon", "SpawnPointGhostCerberus")]
    public async Task ChaplainCanSummonTheConfiguredFamiliarOnlyOnce(string prototype, string expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var chaplain = em.SpawnEntity("MobHuman", map.GridCoords);
            em.AddComponent<BibleUserComponent>(chaplain);
            var book = em.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(chaplain, book), Is.True);
            var summonable = em.GetComponent<SummonableComponent>(book);
            var action = new SummonActionEvent { Performer = chaplain };
            em.EventBus.RaiseLocalEvent(book, action);
            Assert.That(summonable.AlreadySummoned, Is.True);
            Assert.That(summonable.Summon, Is.Not.Null);
            var familiar = summonable.Summon!.Value;
            Assert.That(em.GetComponent<MetaDataComponent>(familiar).EntityPrototype!.ID, Is.EqualTo(expected));
            Assert.That(em.GetComponent<TransformComponent>(familiar).ParentUid, Is.EqualTo(book));
            em.EventBus.RaiseLocalEvent(book, action);
            Assert.That(summonable.Summon, Is.EqualTo(familiar));
            em.DeleteEntity(familiar);
            em.DeleteEntity(book);
            em.DeleteEntity(chaplain);
        });
        await pair.CleanReturnAsync();
    }
}

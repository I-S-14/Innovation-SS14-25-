using Content.Shared.Buckle;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedHumanoidFiltersTest
{
    [TestCase("ToiletEmpty")]
    [TestCase("PrisonToiletEmpty")]
    public async Task HumanoidCanSitOnTheToiletAndCannotBeFlushed(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var toilet = em.SpawnEntity(prototype, map.GridCoords);
            var human = em.SpawnEntity("MobHuman", map.GridCoords);
            var buckle = server.System<SharedBuckleSystem>();
            Assert.That(buckle.TryBuckle(human, human, toilet, popup: false), Is.True);
            Assert.That(buckle.IsBuckled(human), Is.True);
            // Use a standing humanoid so the negative control does not depend on buckle state or cooldown.
            var standingHuman = em.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(buckle.IsBuckled(standingHuman), Is.False);
            var disposals = server.System<SharedDisposalUnitSystem>();
            var unit = em.GetComponent<DisposalUnitComponent>(toilet);
            Assert.That(disposals.TryInsert((toilet, unit), standingHuman, standingHuman), Is.False);
            var item = em.SpawnEntity("Screwdriver", map.GridCoords);
            Assert.That(disposals.TryInsert((toilet, unit), item, human), Is.True);
            Assert.That(unit.Container.ContainedEntities, Does.Contain(item));
            em.DeleteEntity(item);
            em.DeleteEntity(standingHuman);
            em.DeleteEntity(human);
            em.DeleteEntity(toilet);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DnaScramblerAcceptsRetainedHumanoidsAndRejectsHamlet()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var human = em.SpawnEntity("MobHuman", map.GridCoords);
            var hamster = em.SpawnEntity("MobHamsterHamlet", map.GridCoords);
            var implanter = em.SpawnEntity("DnaScramblerImplanter", map.GridCoords);
            var component = em.GetComponent<ImplanterComponent>(implanter);
            var system = server.System<SharedImplanterSystem>();
            Assert.That(system.CanImplant(human, human, implanter, component, out _, out _), Is.True);
            Assert.That(system.CanImplant(human, hamster, implanter, component, out _, out _), Is.False);
            em.DeleteEntity(implanter);
            em.DeleteEntity(hamster);
            em.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}

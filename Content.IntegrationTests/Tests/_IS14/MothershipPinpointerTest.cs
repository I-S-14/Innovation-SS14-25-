// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server._IS14.Cyborgs;
using Content.Shared._IS14.Pinpointer;
using Content.Shared.Pinpointer;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MothershipPinpointerTest
{
    [TestCase("PinpointerMothership")]
    [TestCase("PinpointerMothershipRepaired")]
    [TestCase("IS14CyborgToolXenoPinpointerMothership")]
    public async Task SelectsOneBorgOrTheCoreAndDoesNotSilentlyRetargetOnLoss(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var core = em.SpawnEntity("MothershipCore", map.GridCoords);
            var first = em.SpawnEntity("IS14XenoborgScoutPrinted", map.GridCoords);
            var second = em.SpawnEntity("IS14XenoborgHeavyPrinted", map.GridCoords);
            var human = em.SpawnEntity("MobHuman", map.GridCoords);
            var pointer = em.SpawnEntity(prototype, map.GridCoords);
            var selector = em.GetComponent<MothershipPinpointerComponent>(pointer);
            var tracking = em.GetComponent<PinpointerComponent>(pointer);
            var system = server.System<MothershipPinpointerSystem>();
            Assert.That(selector.Selected, Is.EqualTo(core), "a new pinpointer defaults to the core");
            Assert.That(system.Select((pointer, selector), first), Is.True);
            Assert.That(tracking.IsActive, Is.True);
            Assert.That(tracking.Targets, Is.EquivalentTo(new[] { first }));
            Assert.That(system.Select((pointer, selector), second), Is.True);
            Assert.That(tracking.Targets, Is.EquivalentTo(new[] { second }));
            Assert.That(system.Select((pointer, selector), human), Is.False);
            Assert.That(tracking.Targets, Is.EquivalentTo(new[] { second }));
            em.DeleteEntity(second);
            system.Update(1.1f);
            Assert.That(selector.Selected, Is.Null);
            Assert.That(tracking.Targets, Is.Empty);
            Assert.That(tracking.DistanceToTarget, Is.EqualTo(Distance.Unknown));
            Assert.That(system.Select((pointer, selector), core), Is.True);
            Assert.That(tracking.Targets, Is.EquivalentTo(new[] { core }));
            foreach (var uid in new[] { pointer, human, first, core })
                em.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}

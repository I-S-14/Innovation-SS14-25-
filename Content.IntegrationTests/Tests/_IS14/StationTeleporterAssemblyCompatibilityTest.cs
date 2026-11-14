using System.Linq;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class StationTeleporterAssemblyCompatibilityTest
{
    [Test]
    public async Task TeleporterCreatesItsRealBoardAndRequiredParts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid teleporter = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            teleporter = em.SpawnEntity("StationTeleporter", map.GridCoords);
            var machine = em.GetComponent<MachineComponent>(teleporter);
            Assert.That(machine.BoardContainer.ContainedEntities, Has.Count.EqualTo(1));
            var board = machine.BoardContainer.ContainedEntities.Single();
            Assert.That(em.GetComponent<MetaDataComponent>(board).EntityPrototype!.ID,
                Is.EqualTo("StationTeleporterMachineCircuitboard"));
            Assert.That(em.GetComponent<MachineBoardComponent>(board).Prototype.Id, Is.EqualTo("StationTeleporter"));
            var parts = machine.PartContainer.ContainedEntities.Select(uid => em.GetComponent<StackComponent>(uid))
                .GroupBy(stack => stack.StackTypeId.Id).ToDictionary(group => group.Key, group => group.Sum(stack => stack.Count));
            Assert.That(parts.Keys, Is.EquivalentTo(new[] { "Manipulator", "Cable", "PlasmaGlass" }));
            Assert.That(parts["Manipulator"], Is.EqualTo(3));
            Assert.That(parts["Cable"], Is.EqualTo(5));
            Assert.That(parts["PlasmaGlass"], Is.EqualTo(10));
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var machine = em.GetComponent<MachineComponent>(teleporter);
            foreach (var part in machine.PartContainer.ContainedEntities.ToArray())
                em.DeleteEntity(part);
            foreach (var board in machine.BoardContainer.ContainedEntities.ToArray())
                em.DeleteEntity(board);
            em.DeleteEntity(teleporter);
        });
        await pair.CleanReturnAsync();
    }
}

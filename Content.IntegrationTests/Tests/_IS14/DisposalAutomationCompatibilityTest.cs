using System.Linq;
using Content.Goobstation.Shared.Factory;
using Content.Goobstation.Shared.Factory.Slots;
using Content.Shared.Disposal.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DisposalAutomationCompatibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: DisposalUnit
  id: IS14TestLegacyDisposalContents
  components:
  - type: ContainerContainer
    containers:
      DisposalUnitComponent: !type:Container
      DisposalUnit: !type:Container
      disposals: !type:Container
  - type: ContainerFill
    containers:
      DisposalUnitComponent: [Wrench]
      DisposalUnit: [Crowbar]
      disposals: [Screwdriver]
";

    [Test]
    public async Task LegacyContentsJoinNativeContentsWithoutBeingLost()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var unit = em.SpawnEntity("IS14TestLegacyDisposalContents", map.GridCoords);
            var contents = em.GetComponent<DisposalUnitComponent>(unit).Container.ContainedEntities.ToArray();
            Assert.That(contents.Select(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID),
                Is.EquivalentTo(new[] { "Wrench", "Crowbar", "Screwdriver" }));
            foreach (var item in contents)
                em.DeleteEntity(item);
            em.DeleteEntity(unit);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("DisposalUnit")]
    [TestCase("MailingUnit")]
    public async Task AutomationInsertsIntoTheWorkingDisposalContainer(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var unit = em.SpawnEntity(prototype, map.GridCoords);
            var disposal = em.GetComponent<DisposalUnitComponent>(unit);
            var slot = em.GetComponent<AutomationSlotsComponent>(unit).Slots.OfType<AutomatedContainer>().Single();
            Assert.That(slot.Container, Is.SameAs(disposal.Container));
            var item = em.SpawnEntity("Screwdriver", map.GridCoords);
            Assert.That(slot.Insert(item), Is.True);
            Assert.That(disposal.Container.ContainedEntities, Does.Contain(item));
            em.DeleteEntity(item);
            em.DeleteEntity(unit);
        });
        await pair.CleanReturnAsync();
    }
}

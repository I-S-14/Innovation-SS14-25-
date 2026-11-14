using Content.IntegrationTests.Fixtures;
using Content.Shared.Bed.Components;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class EmagTypeOverrideCompatibilityTest : GameTest
{
    [TestCase(EmagType.Interaction, true)]
    [TestCase(EmagType.Access, false)]
    public async Task OverrideControlsTheActualEffect(EmagType type, bool expected)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var tool = entities.SpawnEntity("Emag", map.GridCoords);
            var bed = entities.SpawnEntity("StasisBed", map.GridCoords);
            Assert.That(entities.GetComponent<EmagComponent>(tool).EmagType, Is.EqualTo(EmagType.Interaction));
            var stasis = entities.GetComponent<StasisBedComponent>(bed);
            var original = stasis.Multiplier;

            var success = Server.System<EmagSystem>().TryEmagEffect(tool, user, bed, type);
            Assert.That(success, Is.EqualTo(expected));
            Assert.That(stasis.Multiplier, Is.EqualTo(expected ? 1f / original : original));
        });
    }
}

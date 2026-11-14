using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ToggleableClothingCompatibilityTest
{
    [TestCase("ClothingOuterSuitEmergency")]
    [TestCase("ClothingOuterWinterHoS")]
    [TestCase("ClothingOuterWinterWarden")]
    [TestCase("ClothingOuterCoatJensen")]
    public async Task ClothingCreatesAttachedPiecesAndTheirAction(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var clothing = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var component = server.EntMan.GetComponent<ToggleableClothingComponent>(clothing);
            var parts = server.System<ToggleableClothingSystem>().GetAttachedClothingsList(clothing);
            Assert.That(parts, Is.Not.Null.And.Not.Empty);
            Assert.That(component.Container.ContainedEntities, Is.EquivalentTo(parts));
            Assert.That(component.ActionEntity, Is.Not.Null);
            server.EntMan.DeleteEntity(clothing);
        });
        await pair.CleanReturnAsync();
    }
}

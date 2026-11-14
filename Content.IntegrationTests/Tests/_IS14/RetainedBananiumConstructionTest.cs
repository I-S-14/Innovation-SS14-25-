using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction;
using Content.Shared.Atmos;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedBananiumConstructionTest
{
    [Test]
    public async Task BuildingBananiumFloorConsumesTwoSheetsAfterTheDelay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default, material = default;
        Task<bool> construction = null!;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false,
                new GasMixture(moles, Atmospherics.T20C));
            var maps = server.System<SharedMapSystem>();
            for (var i = 1; i <= 2; i++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp,
                    map.GridCoords.Offset(new Vector2(i, 0)), map.Tile.Tile);
            var coords = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));
            user = em.SpawnEntity("MobHuman", coords);
            material = em.SpawnEntity("MaterialBananium", coords);
            server.System<SharedStackSystem>().SetCount(material, 3);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, material), Is.True);
            construction = server.System<ConstructionSystem>().TryStartStructureConstruction(user,
                "FloorBananium", coords.Offset(Vector2.UnitX), Angle.Zero);
            Assert.That(construction.IsCompleted, Is.False, "The recipe must retain its real construction delay.");
        });
        await pair.RunSeconds(6);
        await server.WaitAssertion(() => Assert.That(construction.IsCompletedSuccessfully, Is.True));
        Assert.That(await construction, Is.True);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<StackComponent>(material).Count, Is.EqualTo(1));
            var floors = em.EntityQuery<MetaDataComponent>().Where(c =>
                c.EntityPrototype?.ID == "FloorBananiumEntity").ToArray();
            Assert.That(floors, Has.Length.EqualTo(1));
            Assert.That(em.GetComponent<TransformComponent>(floors[0].Owner).Anchored, Is.True);
            em.DeleteEntity(floors[0].Owner);
            em.DeleteEntity(material);
            em.DeleteEntity(user);
        });
        await pair.CleanReturnAsync();
    }
}

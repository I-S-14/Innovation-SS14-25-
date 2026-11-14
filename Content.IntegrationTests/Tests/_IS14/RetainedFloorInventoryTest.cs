using System.Linq;
using System.Numerics;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedFloorInventoryTest
{
    [TestCase("DarkDiagonalMini")]
    [TestCase("DarkDiagonal")]
    [TestCase("DarkOffset")]
    [TestCase("SteelOffset")]
    [TestCase("SteelDiagonalMini")]
    [TestCase("SteelDiagonal")]
    [TestCase("WhiteOffset")]
    [TestCase("WhiteDiagonalMini")]
    [TestCase("WhiteDiagonal")]
    [TestCase("RockVault")]
    [TestCase("GrayConcrete")]
    [TestCase("GrayConcreteMono")]
    [TestCase("GrayConcreteSmooth")]
    [TestCase("OldConcrete")]
    [TestCase("OldConcreteMono")]
    [TestCase("OldConcreteSmooth")]
    [TestCase("IronsandConcrete")]
    [TestCase("IronsandConcreteMono")]
    [TestCase("IronsandConcreteSmooth")]
    [TestCase("ArcadeBlue2")]
    [TestCase("LightAstroGrass")]
    [TestCase("UraniumMarble")]
    [TestCase("DarkSquiggly")]
    [TestCase("ElevatorShaft")]
    public async Task PlacingRemovingAndReusingRetainsTheTile(string suffix)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default, item = default, recovered = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var coords = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            user = em.SpawnEntity("MobHuman", coords);
            item = em.SpawnEntity("FloorTileItem" + suffix, coords);
            var stacks = server.System<SharedStackSystem>();
            stacks.SetCount(item, 2);
            var interaction = new AfterInteractEvent(user, item, null, coords, true);
            em.EventBus.RaiseLocalEvent(item, interaction);
            Assert.That(interaction.Handled, Is.True);
            Assert.That(em.GetComponent<StackComponent>(item).Count, Is.EqualTo(1));
            var maps = server.System<SharedMapSystem>();
            var definitions = server.ResolveDependency<ITileDefinitionManager>();
            var tile = maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, coords);
            Assert.That(definitions[tile.Tile.TypeId].ID, Is.EqualTo("Floor" + suffix));
            var tileSystem = server.System<TileSystem>();
            // Artificial grass is removed with Cutting, rather than a crowbar.
            Assert.That(suffix == "LightAstroGrass"
                ? tileSystem.DeconstructTile(tile)
                : tileSystem.PryTile(tile), Is.True);
            Assert.That(definitions[maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, coords).Tile.TypeId].ID,
                Is.EqualTo("Plating"));
            var drops = em.EntityQuery<StackComponent>().Where(c => c.Owner != item &&
                em.GetComponent<MetaDataComponent>(c.Owner).EntityPrototype?.ID == "FloorTileItem" + suffix).ToArray();
            Assert.That(drops, Has.Length.EqualTo(1));
            recovered = drops[0].Owner;
            Assert.That(drops[0].Count, Is.EqualTo(1));
            Assert.That(drops[0].StackTypeId, Is.EqualTo(em.GetComponent<StackComponent>(item).StackTypeId));
            var reuse = new AfterInteractEvent(user, recovered, null, coords, true);
            em.EventBus.RaiseLocalEvent(recovered, reuse);
            Assert.That(reuse.Handled, Is.True);
            Assert.That(definitions[maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, coords).Tile.TypeId].ID,
                Is.EqualTo("Floor" + suffix));
        });
        await pair.RunTicksSync(3);
        await server.WaitPost(() =>
        {
            foreach (var uid in new[] { user, item, recovered })
                if (server.EntMan.EntityExists(uid))
                    server.EntMan.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}

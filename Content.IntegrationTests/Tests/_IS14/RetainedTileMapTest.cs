using System.Numerics;
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedTileMapTest : GameTest
{
    private static readonly Dictionary<string, (string Parent, float Resistance)> RestoredFloors = new()
    {
        ["FloorDarkDiagonal"] = ("FloorDark", 100),
        ["FloorDarkDiagonalMini"] = ("FloorDark", 100),
        ["FloorDarkOffset"] = ("FloorDark", 100),
        ["FloorWhiteDiagonal"] = ("FloorWhite", 80),
        ["FloorWhiteDiagonalMini"] = ("FloorWhite", 80),
        ["FloorWhiteOffset"] = ("FloorWhite", 80),
        ["FloorEighties"] = ("FloorArcadeBlue", 75),
        ["FloorArcadeBlue2"] = ("FloorArcadeBlue", 75),
        ["FloorHydro"] = ("FloorSteel", 50),
        ["FloorMining"] = ("FloorDarkPlastic", 250),
        ["FloorMiningLight"] = ("FloorDarkPlastic", 250),
        ["FloorMiningDark"] = ("FloorDarkPlastic", 250),
        ["FloorRockVault"] = ("FloorSteel", 400),
        ["FloorShowroom"] = ("FloorSteel", 100),
        ["FloorMono"] = ("FloorSteel", 100),
        ["FloorGrayConcrete"] = ("FloorConcrete", 300),
        ["FloorGrayConcreteMono"] = ("FloorConcreteMono", 300),
        ["FloorGrayConcreteSmooth"] = ("FloorConcreteSmooth", 300),
        ["FloorOldConcrete"] = ("FloorConcrete", 300),
        ["FloorOldConcreteMono"] = ("FloorConcreteMono", 300),
        ["FloorOldConcreteSmooth"] = ("FloorConcreteSmooth", 300),
        ["FloorTechMaint"] = ("FloorTechMaint2", 250),
    };

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task SavingAndLoadingKeepsRetainedGoobTerrain()
    {
        var server = Pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var maps = em.System<SharedMapSystem>();
        var loader = em.System<MapLoaderSystem>();
        var tiles = server.ResolveDependency<ITileDefinitionManager>();
        var resources = server.ResolveDependency<IResourceManager>();
        var path = new ResPath("/Maps/Test/IS14RetainedTerrain.yml");
        string[] identities = ["PlatingBrass", "FloorAsteroidSandRedBorderless", "FloorLowDesert", "FloorSteelDirty",
            "FloorSteelOffset", "FloorSteelDiagonalMini", "FloorSteelDiagonal", "FloorUraniumMarble",
            "FloorLightAstroGrass", "FloorCaveDrought", "FloorIronsandConcrete", "FloorIronsandConcreteMono",
            "FloorIronsandConcreteSmooth", "FloorDarkSquiggly", "FloorElevatorShaft", .. RestoredFloors.Keys];
        await server.WaitAssertion(() =>
        {
            resources.UserData.CreateDir(path.Directory);
            maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var dirty = (ContentTileDefinition) tiles["FloorSteelDirty"];
            Assert.That(dirty.Sprite?.ToString(), Is.EqualTo("/Textures/Tiles/steel_dirty.png"));
            Assert.That(dirty.Variants, Is.EqualTo(1));
            Assert.That(dirty.ItemDropPrototypeName?.Id, Is.EqualTo("FloorTileItemDirty"));
            for (var i = 0; i < identities.Length; i++)
            {
                Assert.That(tiles[identities[i]].ID, Is.EqualTo(identities[i]));
                if (RestoredFloors.TryGetValue(identities[i], out var expected))
                {
                    var restored = (ContentTileDefinition) tiles[identities[i]];
                    var parent = (ContentTileDefinition) tiles[expected.Parent];
                    Assert.That(restored.TileRipResistance, Is.EqualTo(expected.Resistance), identities[i]);
                    Assert.That(restored.Sprite, Is.EqualTo(parent.Sprite), identities[i]);
                    Assert.That(restored.Variants, Is.EqualTo(parent.Variants), identities[i]);
                }
                maps.SetTile(grid, new Vector2i(i, 0), new Tile(tiles[identities[i]].TileId));
            }
            Assert.That(loader.TrySaveMap(mapId, path), Is.True);
            maps.DeleteMap(mapId);
        });
        await server.WaitIdleAsync();
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(path, out var map, out _), Is.True);
            var mapId = map!.Value.Comp.MapId;
            Assert.That(maps.TryFindGridAt(mapId, Vector2.Zero, out var gridUid, out var grid), Is.True);
            for (var i = 0; i < identities.Length; i++)
            {
                var tile = maps.GetTileRef(gridUid, grid!, new Vector2i(i, 0)).Tile;
                Assert.That(tiles[tile.TypeId].ID, Is.EqualTo(identities[i]), "Map aliases must not replace retained terrain.");
            }
            maps.DeleteMap(mapId);
        });
    }
}

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[Category("MapTests")]
public sealed class DevMapTileVariantsCompatibilityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task SavedDevTilesHaveExistingSpriteVariants()
    {
        await Server.WaitAssertion(() =>
        {
            ProtoId<GameMapPrototype> dev = "Dev";
            var grids = Server.System<ServerGameTicker>().LoadGameMap(Server.ProtoMan.Index(dev), out _,
                DeserializationOptions.Default with { InitializeMaps = false });
            Assert.That(grids, Is.Not.Empty);
            var definitions = Server.ResolveDependency<ITileDefinitionManager>();
            var maps = Server.System<SharedMapSystem>();
            var invalid = new List<string>();
            var count = 0;
            foreach (var grid in grids)
            {
                var component = Server.EntMan.GetComponent<MapGridComponent>(grid);
                foreach (var tile in maps.GetAllTiles(grid, component))
                {
                    count++;
                    var definition = definitions[tile.Tile.TypeId];
                    if (tile.Tile.Variant >= definition.Variants && invalid.Count < 20)
                        invalid.Add($"{definition.ID} at {tile.GridIndices}: variant {tile.Tile.Variant}, available {definition.Variants}");
                }
            }
            Assert.That(count, Is.GreaterThan(1000));
            Assert.That(invalid, Is.Empty, "Out-of-range saved variants render as missing-texture checkerboards.");
        });
    }
}

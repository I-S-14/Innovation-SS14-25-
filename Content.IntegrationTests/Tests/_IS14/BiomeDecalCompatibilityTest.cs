using Content.Shared.Decals;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class BiomeDecalCompatibilityTest
{
    [Test]
    public async Task LegacyOwnershipIsRechunkedAndSurvivesSaving()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var serialization = pair.Server.ResolveDependency<ISerializationManager>();
            var entries = new MappingDataNode { { "42", "-17,-1" }, { "1366", "34,20" } };
            var node = new MappingDataNode
            {
                { "decals", new MappingDataNode { { "-24,-8", entries } } },
            };
            var biome = serialization.Read<BiomeComponent>(node);
            var decals = biome.LoadedDecals[new Vector2i(-24, -8)];
            Assert.That(decals, Has.Count.EqualTo(2));
            var negative = new DecalIndex(ChunkEntitySystem.GetChunkIndices(new System.Numerics.Vector2(-17, -1)), 42);
            var positive = new DecalIndex(ChunkEntitySystem.GetChunkIndices(new System.Numerics.Vector2(34, 20)), 1366);
            Assert.That(decals[negative], Is.EqualTo(new Vector2i(-17, -1)));
            Assert.That(decals[positive], Is.EqualTo(new Vector2i(34, 20)));
            var saved = (MappingDataNode) serialization.WriteValue(biome, alwaysWrite: true);
            var restored = serialization.Read<BiomeComponent>(saved);
            Assert.That(restored.LoadedDecals[new Vector2i(-24, -8)], Is.EquivalentTo(decals));

            // A high grid-wide ID may have been remapped by the decal-grid loader.
            // Refuse to truncate it and unload a different decal by mistake.
            entries.Add(new ValueDataNode("70000"), new ValueDataNode("0,0"));
            Assert.That(() => serialization.Read<BiomeComponent>(node), Throws.Exception);
        });
        await pair.CleanReturnAsync();
    }
}

using System.Globalization;
using System.Numerics;
using Content.Shared.Decals;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._IS14.Parallax;

/// <summary>
/// Retains biome ownership of decals saved with grid-wide IDs. Current IDs also
/// identify the chunk entity; dropping this ownership leaves decals behind when
/// a biome chunk unloads.
/// </summary>
public sealed class BiomeDecalsSerializer : ITypeSerializer<Dictionary<Vector2i, Dictionary<DecalIndex, Vector2i>>, MappingDataNode>
{
    public Dictionary<Vector2i, Dictionary<DecalIndex, Vector2i>> Read(
        ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies, SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<Dictionary<Vector2i, Dictionary<DecalIndex, Vector2i>>>? instanceProvider = null)
    {
        var result = new Dictionary<Vector2i, Dictionary<DecalIndex, Vector2i>>();
        foreach (var (chunkNode, entriesNode) in node)
        {
            var chunk = serializationManager.Read<Vector2i>(new ValueDataNode(chunkNode), hookCtx, context);
            var entries = new Dictionary<DecalIndex, Vector2i>();
            foreach (var (indexNode, tileNode) in (MappingDataNode) entriesNode)
            {
                var tile = serializationManager.Read<Vector2i>(tileNode, hookCtx, context);
                DecalIndex index;
                var separator = indexNode.IndexOf(':');
                if (separator < 0)
                {
                    var id = uint.Parse(indexNode, CultureInfo.InvariantCulture);
                    // The legacy decal-grid loader preserves these IDs unchanged.
                    // Larger IDs require its remapping table, so never silently
                    // truncate them or point biome cleanup at a different decal.
                    if (id > DecalChunkComponent.MaxServerDecalId)
                        throw new InvalidOperationException($"Legacy biome decal {id} requires explicit decal-grid remapping.");
                    index = new DecalIndex(ChunkEntitySystem.GetChunkIndices(new Vector2(tile.X, tile.Y)), (ushort) id);
                }
                else
                {
                    index = new DecalIndex(
                        serializationManager.Read<Vector2i>(new ValueDataNode(indexNode[..separator]), hookCtx, context),
                        ushort.Parse(indexNode[(separator + 1)..], CultureInfo.InvariantCulture));
                }
                entries.Add(index, tile);
            }
            result.Add(chunk, entries);
        }
        return result;
    }

    public DataNode Write(ISerializationManager serializationManager,
        Dictionary<Vector2i, Dictionary<DecalIndex, Vector2i>> value,
        IDependencyCollection dependencies, bool alwaysWrite = false, ISerializationContext? context = null)
    {
        var result = new MappingDataNode();
        foreach (var (chunk, entries) in value)
        {
            var decals = new MappingDataNode();
            foreach (var (index, tile) in entries)
            {
                // MappingDataNode keys are scalars in the published engine.
                // DecalIndex.ToString includes both the chunk and local ID.
                decals.Add(index.ToString(), serializationManager.WriteValue(tile, alwaysWrite, context));
            }
            result.Add(((ValueDataNode) serializationManager.WriteValue(chunk, alwaysWrite, context)).Value, decals);
        }
        return result;
    }

    public ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        // No prototype references. Typed coordinate/index validation happens in Read,
        // matching the legacy decal-grid serializer's validation of saved map data.
        return new InconclusiveNode(node);
    }
}

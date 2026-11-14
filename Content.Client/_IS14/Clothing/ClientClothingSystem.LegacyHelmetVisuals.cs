using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Clothing.Components;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.Clothing;

public sealed partial class ClientClothingSystem
{
    private bool HasClothingStates(Entity<ClothingComponent> clothing, List<PrototypeLayerData> layers)
    {
        RSI? fallback = null;
        if (clothing.Comp.RsiPath != null)
            fallback = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / clothing.Comp.RsiPath).RSI;
        else if (_spriteQuery.TryComp(clothing, out var sprite))
            fallback = sprite.BaseRSI;
        foreach (var layer in layers)
        {
            if (layer.State == null || layer.TexturePath != null)
                continue;
            var rsi = layer.RsiPath == null ? fallback
                : _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / layer.RsiPath).RSI;
            if (rsi == null || !rsi.TryGetState(layer.State, out _))
                return false;
        }
        return true;
    }

    private bool TryGetLegacyHelmetVisuals(Entity<ClothingComponent> clothing, string slot, string? species,
        List<PrototypeLayerData> declared, [NotNullWhen(true)] out List<PrototypeLayerData>? layers)
    {
        layers = null;
        if (slot != "head" || declared.Count == 0)
            return false;

        foreach (var layer in declared)
        {
            if (layer.State == null || layer.RsiPath != null || layer.TexturePath != null)
                return false;
            var state = layer.State;
            if (species != null && state.EndsWith("-" + species))
                state = state[..^(species.Length + 1)];
            if (state is not ("equipped-head" or "equipped-head-unshaded" or "equipped-helmet" or "equipped-HELMET"))
                return false;
        }

        if (!TryGetDefaultVisuals(clothing, slot, species, out var legacy))
            return false;
        var rsi = _cache.GetResource<RSIResource>(legacy[0].RsiPath!).RSI;
        if (declared.All(layer => rsi.TryGetState(layer.State!, out _)))
            return false;

        // The inherited modern layout is incompatible with this legacy RSI.
        // The existing default resolver preserves on/off prefixes and species art.
        layers = legacy;
        return true;
    }
}

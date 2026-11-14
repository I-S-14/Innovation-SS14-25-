using Content.Shared.Store.Components;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Store;

/// <summary>
/// Supplies the retained Goob device-bound uplink with the preset no longer inherited by Wizards PDAs.
/// </summary>
public sealed class RetainedUplinkStoreSystem : EntitySystem
{
    private const string Preset = "IS14RetainedUplinkStorePreset";
    [Dependency] private readonly SharedStoreSystem _stores = default!;

    public StoreComponent EnsureStore(EntityUid uid)
    {
        if (TryComp<StoreComponent>(uid, out var existing))
        {
            // Retained implants can have a balance-only Store after losing their old preset parent.
            if (existing.Categories.Count == 0 &&
                ProtoMan.Index<EntityPrototype>(Preset).TryGetComponent<StoreComponent>(out var preset))
            {
                existing.Categories.UnionWith(preset.Categories);
                existing.CurrencyWhitelist.UnionWith(preset.CurrencyWhitelist);
                if (existing.Name == "store-ui-default-title")
                    existing.Name = preset.Name;
                _stores.RefreshAllListings(existing);
                Dirty(uid, existing);
            }

            return existing;
        }

        // Add configured components before startup refreshes the catalogue and registers the ringer code.
        EntityManager.AddComponents(uid, ProtoMan.Index<EntityPrototype>(Preset).Components);
        return Comp<StoreComponent>(uid);
    }
}

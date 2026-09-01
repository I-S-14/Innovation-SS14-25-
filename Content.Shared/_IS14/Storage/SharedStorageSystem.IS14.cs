// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared.Storage.EntitySystems;

/// <summary>
///     IS14 additions to storage, for grids handed out at runtime rather than declared on
///     a prototype — a MOD module opening compartments across the suit, for one.
/// </summary>
public abstract partial class SharedStorageSystem
{
    /// <summary>
    ///     Sets the largest item a container will take.
    /// </summary>
    /// <remarks>
    ///     The field is access-locked to this system, so runtime-granted storage cannot
    ///     set its own limit. Without this a grid hung off a small entity derives its
    ///     limit from that entity and refuses everything a bag that size would obviously
    ///     hold.
    /// </remarks>
    public void SetMaxItemSize(Entity<StorageComponent> ent, ProtoId<ItemSizePrototype>? size)
    {
        ent.Comp.MaxItemSize = size;
        Dirty(ent);
    }
}

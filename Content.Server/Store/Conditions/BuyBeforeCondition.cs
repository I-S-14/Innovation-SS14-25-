using System.Linq;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Store.Conditions;

public sealed partial class BuyBeforeCondition : ListingCondition
{
    /// <summary>
    ///     Required listing(s) needed to purchase before this listing is available
    /// </summary>
    [DataField] // Goob edit
    public HashSet<ProtoId<ListingPrototype>>? Whitelist; // Goob edit

    /// <summary>
    ///     Goobstation.
    ///     If false, only one of the listings needs to be purchased to pass the whitelist.
    ///     If true, all of the listings need to be purchased.
    /// </summary>
    [DataField]
    public bool WhitelistRequireAll = true;

    /// <summary>
    ///     Listing(s) that if bought, block this purchase, if any.
    /// </summary>
    [DataField] // Goobstation
    public HashSet<ProtoId<ListingPrototype>>? Blacklist;

    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.EntityManager.TryGetComponent<StoreComponent>(args.StoreEntity, out var storeComp))
            return false;

        var allListings = storeComp.FullListingsCatalog;

        if (Blacklist != null && allListings.Any(listing => Blacklist.Contains(listing.ID) && listing.PurchaseAmount > 0))
            return false;

        if (Whitelist == null || Whitelist.Count == 0)
            return true;

        bool Purchased(ProtoId<ListingPrototype> id) =>
            allListings.Any(listing => listing.ID == id && listing.PurchaseAmount > 0);

        return WhitelistRequireAll ? Whitelist.All(Purchased) : Whitelist.Any(Purchased);
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Construction;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._IS14.Modsuit.Completions;

/// <summary>
///     Finishes a MOD assembly: turns the shell into the suit its plating calls for and
///     carries the seated core across.
///
///     The graph cannot do this with a plain <c>entity:</c> node the way most recipes do,
///     because which suit comes out is not known until the plating goes in. Branching the
///     graph per theme would answer that, but it costs the player the "next step" hint —
///     a node with eleven outgoing edges has no single next step to name — so the theme
///     is read off the plating here instead, and the graph stays one edge wide.
///
///     Modelled on <see cref="BuildMech"/>, which solves the same problem for exosuits.
/// </summary>
[UsedImplicitly, DataDefinition]
public sealed partial class BuildModsuit : IGraphAction
{
    /// <summary>Container the plating was stored in by its construction step.</summary>
    [DataField]
    public string PlatingContainer = "modsuit-plating";

    /// <summary>Container the core was stored in. Named after the suit's own core slot.</summary>
    [DataField]
    public string CoreContainer = "mod-core";

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        var containers = entityManager.System<ContainerSystem>();

        if (!containers.TryGetContainer(uid, PlatingContainer, out var platingContainer)
            || platingContainer.ContainedEntities.Count == 0)
        {
            Logger.Warning($"MOD assembly {uid} finished without a plating in '{PlatingContainer}'! Aborting.");
            return;
        }

        var plating = platingContainer.ContainedEntities[0];

        if (!entityManager.TryGetComponent<ModsuitAssemblyPlatingComponent>(plating, out var platingComp))
        {
            Logger.Warning($"MOD assembly {uid} had a plating without a result prototype! Aborting.");
            return;
        }

        var coordinates = entityManager.GetComponent<TransformComponent>(uid).Coordinates;
        var suit = entityManager.SpawnEntity(platingComp.Result, coordinates);

        // Carry the core over. MOD prototypes no longer start with one — the only core in
        // the round is the one somebody bought — so this is the suit's.
        if (containers.TryGetContainer(uid, CoreContainer, out var coreContainer)
            && coreContainer.ContainedEntities.Count > 0)
        {
            var core = coreContainer.ContainedEntities[0];
            containers.Remove(core, coreContainer);

            if (!entityManager.System<ItemSlotsSystem>().TryInsert(suit, CoreContainer, core, user: null))
            {
                // Should not happen — the slot is empty on a fresh suit — but a core is
                // expensive enough that losing one to a silent failure is worse than
                // dropping it at the builder's feet.
                entityManager.System<SharedTransformSystem>().DropNextTo(core, suit);
            }
        }

        var entChangeEv = new ConstructionChangeEntityEvent(suit, uid);
        entityManager.EventBus.RaiseLocalEvent(uid, entChangeEv);
        entityManager.EventBus.RaiseLocalEvent(suit, entChangeEv, broadcast: true);

        entityManager.QueueDeleteEntity(uid);

        // Straight into the builder's hands, the way a finished node entity would be.
        if (userUid is { } user)
            entityManager.System<SharedHandsSystem>().TryPickupAnyHand(user, suit);
    }
}

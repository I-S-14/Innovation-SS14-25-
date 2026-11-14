// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions.Components;
using Content.Shared.Inventory;
using Content.Shared.Whitelist;

namespace Content.Shared.Actions;

/// <summary>
/// <see cref="ActionGrantComponent"/>
/// </summary>
public sealed partial class ActionGrantSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    [SubscribeLocalEvent]
    private void OnItemGet(Entity<ItemActionGrantComponent> ent, ref GetItemActionsEvent args)
    {
        if (!TryComp(ent.Owner, out ActionGrantComponent? grant))
            return;

        // IS14-change: retain Wizards' worn-only check and Goob's optional slot mask.
        if (ent.Comp.ActiveIfWorn)
        {
            if (args.SlotFlags is not { } slot || (slot & ~SlotFlags.POCKET) == SlotFlags.NONE)
                return;

            if (ent.Comp.RestrictedSlots is { } restricted && (restricted & slot) == SlotFlags.NONE)
                return;
        }

        foreach (var action in grant.ActionEntities)
        {
            if (TryComp<ActionUserWhitelistComponent>(action, out var whitelist) &&
                !_whitelist.IsWhitelistPass(whitelist.Whitelist, args.User))
            {
                continue;
            }

            args.AddAction(action);
        }
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<ActionGrantComponent> ent, ref MapInitEvent args)
    {
        foreach (var action in ent.Comp.Actions)
        {
            EntityUid? actionEnt = null;
            _actions.AddAction(ent.Owner, ref actionEnt, action);

            if (actionEnt != null)
                ent.Comp.ActionEntities.Add(actionEnt.Value);
        }

        Dirty(ent); // Goobstation
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<ActionGrantComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.RemoveOnShutdown)
            return;

        foreach (var actionEnt in ent.Comp.ActionEntities)
        {
            _actions.RemoveAction(ent.Owner, actionEnt);
        }
    }
}

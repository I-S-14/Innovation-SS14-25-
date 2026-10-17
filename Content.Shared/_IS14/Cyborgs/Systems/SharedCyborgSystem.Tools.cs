// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Item;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Systems;

public abstract partial class SharedCyborgSystem
{
    private void InitializeTools()
    {
        SubscribeAllEvent<CyborgToggleToolRequest>(OnToggleToolRequest);
        SubscribeAllEvent<CyborgStoreToolRequest>(OnStoreToolRequest);
        SubscribeAllEvent<CyborgOpenModelMenuRequest>(OnOpenModelMenuRequest);
        SubscribeLocalEvent<CyborgComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<CyborgComponent, ContainerIsInsertingAttemptEvent>(OnSlotInsertAttempt);
    }

    // Like tg, a cyborg's hands are its module slots: it can't pick up anything but its own tools
    // and what its model allows, like the engineering cyborg's sheets. Deploying a tool uses DoPickup,
    // which skips this check.
    private void OnPickupAttempt(Entity<CyborgComponent> ent, ref PickupAttemptEvent args)
    {
        if (CompOrNull<CyborgToolComponent>(args.Item)?.Cyborg == ent.Owner)
            return;

        if (!HasComp<CyborgToolComponent>(args.Item) && CanPickUp(ent.Comp, args.Item))
            return;

        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupClient(Loc.GetString("cyborg-cannot-pickup"), ent, ent);
    }

    /// <summary>
    /// Whether the cyborg's model lets it hold this item in a module slot.
    /// </summary>
    public bool CanPickUp(CyborgComponent cyborg, EntityUid item)
    {
        if (cyborg.Model == null || !_proto.TryIndex(cyborg.Model, out var model))
            return false;

        if (model.PickupStacks.Count > 0 && TryComp<StackComponent>(item, out var stack) &&
            model.PickupStacks.Contains(stack.StackTypeId))
            return true;

        return _whitelist.IsWhitelistPass(model.PickupWhitelist, item);
    }

    // An offline slot holds nothing, picked up or deployed.
    private void OnSlotInsertAttempt(Entity<CyborgComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var id = args.Container.ID;
        if (TryGetToolSlotIndex(ent.Comp, id, out var index) && !IsSlotWorking(ent.Comp, index))
            args.Cancel();
    }

    /// <summary>
    /// The slot number behind a tool hand id, the reverse of <see cref="GetToolHandId"/>.
    /// </summary>
    public bool TryGetToolSlotIndex(CyborgComponent cyborg, string handId, out int index)
    {
        index = -1;
        return handId.StartsWith(cyborg.ToolHandPrefix + "-") &&
               int.TryParse(handId.AsSpan(cyborg.ToolHandPrefix.Length + 1), out index);
    }

    /// <summary>
    /// Empties a module slot: a tool goes back into the chassis, anything picked up is dropped.
    /// </summary>
    public void ClearSlot(Entity<CyborgComponent> ent, EntityUid held)
    {
        if (HasComp<CyborgToolComponent>(held))
            StoreTool(ent, held);
        else
            _hands.TryDrop(ent.Owner, held, checkActionBlocker: false);
    }

    private bool TryGetSenderCyborg(EntitySessionEventArgs args, out Entity<CyborgComponent> cyborg)
    {
        cyborg = default;
        if (args.SenderSession.AttachedEntity is not { } uid || !TryComp<CyborgComponent>(uid, out var comp))
            return false;

        cyborg = (uid, comp);
        return true;
    }

    // Clicked in the HUD tool grid: take it out, or put it back if it is already held.
    private void OnToggleToolRequest(CyborgToggleToolRequest msg, EntitySessionEventArgs args)
    {
        if (!TryGetSenderCyborg(args, out var ent))
            return;

        if (!TryGetEntity(msg.Tool, out var tool) || !TryComp<CyborgToolComponent>(tool, out var toolComp) ||
            toolComp.Cyborg != ent.Owner)
            return;

        if (IsToolHeld(ent, tool.Value, out _))
            StoreTool(ent, tool.Value);
        else
            TryDeployTool(ent, (tool.Value, toolComp));
    }

    // The store button or a right click on a slot: put that slot's tool back, the active one by default.
    private void OnStoreToolRequest(CyborgStoreToolRequest msg, EntitySessionEventArgs args)
    {
        if (!TryGetSenderCyborg(args, out var ent))
            return;

        var hand = msg.Hand ?? CompOrNull<HandsComponent>(ent)?.ActiveHandId;
        if (hand == null || !hand.StartsWith(ent.Comp.ToolHandPrefix))
            return;

        if (_hands.TryGetHeldItem(ent.Owner, hand, out var held))
            ClearSlot(ent, held.Value);
    }

    // A cyborg that closed the model picker without choosing gets it back from the HUD.
    private void OnOpenModelMenuRequest(CyborgOpenModelMenuRequest msg, EntitySessionEventArgs args)
    {
        if (TryGetSenderCyborg(args, out var ent) && ent.Comp.Model == null)
            _ui.TryOpenUi(ent.Owner, CyborgUiKey.Model, ent.Owner);
    }

    public string GetToolHandId(CyborgComponent cyborg, int index)
    {
        return $"{cyborg.ToolHandPrefix}-{index}";
    }

    /// <summary>
    /// All tools of the cyborg, stored and held, that it is allowed to see.
    /// </summary>
    public IEnumerable<EntityUid> EnumerateTools(Entity<CyborgComponent> ent)
    {
        foreach (var tool in ent.Comp.ToolContainer.ContainedEntities)
        {
            if (IsToolAvailable(ent.Comp, tool))
                yield return tool;
        }

        for (var i = 0; i < ent.Comp.ToolSlots; i++)
        {
            if (_hands.TryGetHeldItem(ent.Owner, GetToolHandId(ent.Comp, i), out var held) &&
                HasComp<CyborgToolComponent>(held))
                yield return held.Value;
        }
    }

    private bool IsToolAvailable(CyborgComponent cyborg, EntityUid tool)
    {
        return TryComp<CyborgToolComponent>(tool, out var toolComp) && (!toolComp.EmagTool || cyborg.EmagToolsUnlocked);
    }

    public bool IsToolHeld(Entity<CyborgComponent> ent, EntityUid tool, [NotNullWhen(true)] out string? hand)
    {
        for (var i = 0; i < ent.Comp.ToolSlots; i++)
        {
            var handId = GetToolHandId(ent.Comp, i);
            if (_hands.TryGetHeldItem(ent.Owner, handId, out var held) && held == tool)
            {
                hand = handId;
                return true;
            }
        }

        hand = null;
        return false;
    }

    /// <summary>
    /// Moves a stored tool into the first free tool slot.
    /// </summary>
    public bool TryDeployTool(Entity<CyborgComponent> ent, Entity<CyborgToolComponent> tool)
    {
        if (!ent.Comp.Active)
        {
            _popup.PopupClient(Loc.GetString("cyborg-tools-no-power"), ent.Owner, ent.Owner);
            return false;
        }

        if (!ent.Comp.ToolContainer.Contains(tool) || !IsToolAvailable(ent.Comp, tool))
            return false;

        if (ent.Comp.TransformEnd != null)
            return false;

        if (CompOrNull<CyborgTippableComponent>(ent)?.Tipped == true)
        {
            _popup.PopupClient(Loc.GetString("cyborg-tipped-no-tools"), ent.Owner, ent.Owner);
            return false;
        }

        // The active slot first, so the player decides where a module goes; then the first free one.
        string? freeHand = null;
        if (CompOrNull<HandsComponent>(ent)?.ActiveHandId is { } active &&
            TryGetToolSlotIndex(ent.Comp, active, out var activeIndex) && IsSlotWorking(ent.Comp, activeIndex) &&
            _hands.HandIsEmpty(ent.Owner, active))
        {
            freeHand = active;
        }

        for (var i = 0; freeHand == null && i < ent.Comp.ToolSlots - ent.Comp.BrokenSlots; i++)
        {
            var handId = GetToolHandId(ent.Comp, i);
            if (_hands.HandIsEmpty(ent.Owner, handId))
            {
                freeHand = handId;
                break;
            }
        }

        if (freeHand == null)
        {
            _popup.PopupClient(Loc.GetString(ent.Comp.BrokenSlots >= ent.Comp.ToolSlots
                ? "cyborg-slots-all-offline"
                : "cyborg-tools-slots-full"), ent.Owner, ent.Owner);
            return false;
        }

        // DoPickup takes the tool out of the storage container.
        _hands.DoPickup(ent.Owner, freeHand, tool.Owner);
        _hands.SetActiveHand(ent.Owner, freeHand);

        var unremoveable = EnsureComp<UnremoveableComponent>(tool);
        unremoveable.DeleteOnDrop = false;
        Dirty(tool.Owner, unremoveable);
        return true;
    }

    /// <summary>
    /// Puts a held tool back into the storage.
    /// </summary>
    public void StoreTool(Entity<CyborgComponent> ent, EntityUid tool)
    {
        // A stowed welder or energy blade doesn't keep burning inside the chassis.
        _itemToggle.TryDeactivate(tool, ent.Owner, showPopup: false);
        RemComp<UnremoveableComponent>(tool);
        _container.Insert(tool, ent.Comp.ToolContainer);
    }

    public void StoreAllTools(Entity<CyborgComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        for (var i = 0; i < ent.Comp.ToolSlots; i++)
        {
            if (_hands.TryGetHeldItem(ent.Owner, GetToolHandId(ent.Comp, i), out var held))
                ClearSlot(ent, held.Value);
        }
    }

    private void SpawnTool(Entity<CyborgComponent> ent, EntProtoId proto, bool emagTool, EntityUid? source = null)
    {
        var tool = Spawn(proto, Transform(ent.Owner).Coordinates);
        var toolComp = EnsureComp<CyborgToolComponent>(tool);
        toolComp.Cyborg = ent.Owner;
        toolComp.EmagTool = emagTool;
        toolComp.SourceUpgrade = source;
        Dirty(tool, toolComp);

        if (!_container.Insert(tool, ent.Comp.ToolContainer))
        {
            Log.Error($"Failed to insert cyborg tool {ToPrettyString(tool)} into {ToPrettyString(ent.Owner)}");
            QueueDel(tool);
        }
    }

    private void DeleteAllTools(Entity<CyborgComponent> ent)
    {
        StoreAllTools(ent);

        foreach (var tool in new List<EntityUid>(ent.Comp.ToolContainer.ContainedEntities))
        {
            PredictedDel(tool);
        }
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Emag.Components;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.VendingMachines.Components;
using Content.Shared.VendingMachines.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.VendingMachines;

public abstract partial class SharedVendingMachineSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPowerReceiverSystem _receiver = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SharedUserInterfaceSystem UISystem = default!;
    [Dependency] protected IRobustRandom Randomizer = default!;
    [Dependency] private EmagSystem _emag = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<VendingMachineComponent>(VendingMachineUiKey.Key, subs =>
        {
            subs.Event<VendingMachineEjectMessage>(OnInventoryEjectMessage);
        });
    }

    [SubscribeLocalEvent]
    private void OnOpenVendingUIAction(Entity<VendingMachineComponent> entity, ref OpenVendingUIActionEvent args)
    {
        UISystem.TryToggleUi(entity.Owner, VendingMachineUiKey.Key,args.Performer);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnVendingGetState(Entity<VendingMachineComponent> entity, ref ComponentGetState args)
    {
        var component = entity.Comp;
        var state = new VendingMachineComponentState
        {
            Contraband = component.Contraband,
            Broken = component.Broken,
        };

        CopyInventory(component.Inventory, state.Inventory);
        CopyInventory(component.EmaggedInventory, state.EmaggedInventory);
        CopyInventory(component.ContrabandInventory, state.ContrabandInventory);

        args.State = state;
    }

    protected static void CopyInventory(
        Dictionary<string, VendingMachineInventoryEntry> source,
        Dictionary<string, VendingMachineInventoryEntry> target)
    {
        target.Clear();

        foreach (var entry in source)
        {
            target.Add(entry.Key, new(entry.Value));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VendingMachineComponent, VendingMachineEjectComponent>();
        var curTime = Timing.CurTime;

        while (query.MoveNext(out var uid, out var comp, out var eject))
        {
            UpdateEjectState((uid, comp, eject), curTime);
        }
    }

    [SubscribeLocalEvent]
    protected virtual void OnMapInit(EntityUid uid, VendingMachineComponent component, MapInitEvent args)
    {
        RestockInventoryFromPrototype(uid, component, component.InitialStockQuality);
    }

    private void OnEmpPulse(Entity<VendingMachineComponent> ent, ref EmpPulseEvent args)
    {
        if (!ent.Comp.Broken && _receiver.IsPowered(ent.Owner))
        {
            args.Affected = true;
            args.Disabled = true;
            ent.Comp.NextEmpEject = Timing.CurTime;
        }
    }

    protected virtual void EjectItem(EntityUid uid, VendingMachineComponent? vendComponent = null, bool forceEject = false) { }

    /// <summary>
    /// Checks if the user is authorized to use this vending machine
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="sender">Entity trying to use the vending machine</param>
    /// <param name="vendComponent"></param>
    public bool IsAuthorized(EntityUid uid, EntityUid sender, VendingMachineComponent? vendComponent = null)
    {
        if (!Resolve(uid, ref vendComponent))
            return false;

        if (!TryComp<AccessReaderComponent>(uid, out var accessReader))
            return true;

        if (_accessReader.IsAllowed(sender, uid, accessReader) || HasComp<EmaggedComponent>(uid))
            return true;

        Popup.PopupClient(Loc.GetString("vending-machine-component-try-eject-access-denied"), uid, sender);
        Deny((uid, vendComponent), sender);
        return false;
    }

    protected VendingMachineInventoryEntry? GetEntry(EntityUid uid, string entryId, InventoryType type, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return null;

        if (type == InventoryType.Emagged && HasComp<EmaggedComponent>(uid))
            return component.EmaggedInventory.GetValueOrDefault(entryId);

        if (type == InventoryType.Contraband && component.Contraband)
            return component.ContrabandInventory.GetValueOrDefault(entryId);

        return component.Inventory.GetValueOrDefault(entryId);
    }

    /// <summary>
    /// Tries to eject the provided item. Will do nothing if the vending machine is incapable of ejecting, already ejecting
    /// or the item doesn't exist in its inventory.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="type">The type of inventory the item is from</param>
    /// <param name="itemId">The prototype ID of the item</param>
    /// <param name="throwItem">Whether the item should be thrown in a random direction after ejection</param>
    /// <param name="vendComponent"></param>
    public void TryEjectVendorItem(EntityUid uid, InventoryType type, string itemId, bool throwItem, EntityUid? user = null, VendingMachineComponent? vendComponent = null)
    {
        if (!Resolve(uid, ref vendComponent))
            return;

        if (vendComponent.Ejecting || vendComponent.Broken || !_receiver.IsPowered(uid))
        {
            return;
        }

        var entry = GetEntry(uid, itemId, type, vendComponent);

        if (string.IsNullOrEmpty(entry?.ID))
        {
            Popup.PopupClient(Loc.GetString("vending-machine-component-try-eject-invalid-item"), uid);
            Deny((uid, vendComponent));
            return;
        }

        // Synchronize returned-item stock before the availability check.
        var cleanedReturnedInventory = CleanupReturnedInventoryBeforeVend(uid, vendComponent, itemId); // CorvaxGoob - Vending Return

        if (entry.Amount <= 0)
        {
            // CorvaxGoob Edit Start - Vending Return
            if (cleanedReturnedInventory)
            {
                Dirty(uid, vendComponent);
                UpdateUI((uid, vendComponent));
            }
            // CorvaxGoob End

            Popup.PopupClient(Loc.GetString("vending-machine-component-try-eject-out-of-stock"), uid);
            Deny((uid, vendComponent));
            return;
        }

        // Start Ejecting, and prevent users from ordering while anim playing
        vendComponent.EjectEnd = Timing.CurTime + vendComponent.EjectDelay;
        vendComponent.NextItemToEject = entry.ID;
        vendComponent.ThrowNextItem = throwItem;

        if (TryComp(uid, out SpeakOnUIClosedComponent? speakComponent))
            _speakOn.TrySetFlag((uid, speakComponent));

        entry.Amount--;
        Dirty(uid, vendComponent);
        UpdateUI((uid, vendComponent));
        TryUpdateVisualState((uid, vendComponent));
        Audio.PlayPredicted(vendComponent.SoundVend, uid, user);
    }

    public void Deny(Entity<VendingMachineComponent?> entity, EntityUid? user = null)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        if (entity.Comp.Denying)
            return;

        entity.Comp.DenyEnd = Timing.CurTime + entity.Comp.DenyDelay;
        Audio.PlayPredicted(entity.Comp.SoundDeny, entity.Owner, user, AudioParams.Default.WithVolume(-2f));
        TryUpdateVisualState(entity);
        Dirty(entity);
    }

    protected virtual void UpdateUI(Entity<VendingMachineComponent?> entity) { }

    /// <summary>
    /// Tries to update the visuals of the component based on its current state.
    /// </summary>
    public void TryUpdateVisualState(Entity<VendingMachineComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        var finalState = VendingMachineVisualState.Normal;
        if (entity.Comp.Broken)
        {
            finalState = VendingMachineVisualState.Broken;
        }
        else if (entity.Comp.Ejecting)
        {
            finalState = VendingMachineVisualState.Eject;
        }
        else if (entity.Comp.Denying)
        {
            finalState = VendingMachineVisualState.Deny;
        }
        else if (!_receiver.IsPowered(entity.Owner))
        {
            finalState = VendingMachineVisualState.Off;
        }

        // TODO: You know this should really live on the client with netsync off because client knows the state.
        if (Light.TryGetLight(entity.Owner, out var pointlight))
        {
            var lightEnabled = finalState != VendingMachineVisualState.Broken && finalState != VendingMachineVisualState.Off;
            Light.SetEnabled(entity.Owner, lightEnabled, pointlight);
        }

        _appearanceSystem.SetData(entity.Owner, VendingMachineVisuals.VisualState, finalState);
    }

    /// <summary>
    /// Checks whether the user is authorized to use the vending machine, then ejects the provided item if true
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="sender">Entity that is trying to use the vending machine</param>
    /// <param name="type">The type of inventory the item is from</param>
    /// <param name="itemId">The prototype ID of the item</param>
    /// <param name="component"></param>
    public void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId, VendingMachineComponent component)
    {
        if (IsAuthorized(uid, sender, component))
        {
            TryEjectVendorItem(uid, type, itemId, component.CanShoot, sender, component);
        }
    }

    public void RestockInventoryFromPrototype(EntityUid uid,
        VendingMachineComponent? component = null, float restockQuality = 1f)
    {
        if (!Resolve(uid, ref component))
        {
            return;
        }

        if (!PrototypeManager.TryIndex(component.PackPrototypeId, out VendingMachineInventoryPrototype? packPrototype))
            return;

        AddInventoryFromPrototype(uid, packPrototype.StartingInventory, InventoryType.Regular, component, restockQuality);
        AddInventoryFromPrototype(uid, packPrototype.EmaggedInventory, InventoryType.Emagged, component, restockQuality);
        AddInventoryFromPrototype(uid, packPrototype.ContrabandInventory, InventoryType.Contraband, component, restockQuality);
        Dirty(uid, component);
    }

    private void OnEmagged(EntityUid uid, VendingMachineComponent component, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        // only emag if there are emag-only items
        args.Handled = component.EmaggedInventory.Count > 0;
    }

    [SubscribeLocalEvent]
    private void OnActivatableUIOpenAttempt(EntityUid uid, VendingMachineComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (component.Broken)
            args.Cancel();
    }

    [SubscribeLocalEvent]
    private void OnBreak(EntityUid uid, VendingMachineComponent vendComponent, BreakageEventArgs eventArgs)
    {
        vendComponent.Broken = true;
        Dirty(uid, vendComponent);

        UISystem.CloseUi(uid, VendingMachineUiKey.Key);
    }

    protected virtual void UpdateUI(Entity<VendingMachineComponent?> entity) { }

    public void RestockInventoryFromPrototype(EntityUid uid,
        VendingMachineComponent? component = null, float restockQuality = 1f)
    {
        if (!Resolve(uid, ref component))
        {
            return;
        }

        if (!ProtoMan.TryIndex(component.PackPrototypeId, out VendingMachineInventoryPrototype? packPrototype))
            return;

        AddInventoryFromPrototype(uid, packPrototype.StartingInventory, InventoryType.Regular, component, restockQuality);
        AddInventoryFromPrototype(uid, packPrototype.EmaggedInventory, InventoryType.Emagged, component, restockQuality);
        AddInventoryFromPrototype(uid, packPrototype.ContrabandInventory, InventoryType.Contraband, component, restockQuality);
        Dirty(uid, component);
    }

    /// <summary>
    /// Returns all of the vending machine's inventory. Only includes emagged and contraband inventories if
    /// <see cref="EmaggedComponent"/> with the EmagType.Interaction flag exists and <see cref="VendingMachineComponent.Contraband"/> is true
    /// are <c>true</c> respectively.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public List<VendingMachineInventoryEntry> GetAllInventory(EntityUid uid, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        var inventory = new List<VendingMachineInventoryEntry>(component.Inventory.Values);

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            inventory.AddRange(component.EmaggedInventory.Values);

        if (component.Contraband)
            inventory.AddRange(component.ContrabandInventory.Values);

        return inventory;
    }

    public List<VendingMachineInventoryEntry> GetAvailableInventory(EntityUid uid, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return new();

        return GetAllInventory(uid, component).Where(_ => _.Amount > 0).ToList();
    }

    private void AddInventoryFromPrototype(EntityUid uid, Dictionary<EntProtoId, uint>? entries,
        InventoryType type,
        VendingMachineComponent? component = null, float restockQuality = 1.0f)
    {
        if (!Resolve(uid, ref component) || entries == null)
        {
            return;
        }

        Dictionary<string, VendingMachineInventoryEntry> inventory;
        switch (type)
        {
            case InventoryType.Regular:
                inventory = component.Inventory;
                break;
            case InventoryType.Emagged:
                inventory = component.EmaggedInventory;
                break;
            case InventoryType.Contraband:
                inventory = component.ContrabandInventory;
                break;
            default:
                return;
        }

        foreach (var (id, amount) in entries)
        {
            if (!ProtoMan.HasIndex<EntityPrototype>(id)) continue;
            var restock = amount;
            var chanceOfMissingStock = 1 - restockQuality;

            var result = Randomizer.NextFloat(0, 1);
            if (result < chanceOfMissingStock)
            {
                restock = (uint) Math.Floor(amount * result / chanceOfMissingStock);
            }

            if (inventory.TryGetValue(id, out var entry))
                // Prevent a machine's stock from going over three times
                // the prototype's normal amount. This is an arbitrary
                // number and meant to be a convenience for someone
                // restocking a machine who doesn't want to force vend out
                // all the items just to restock one empty slot without
                // losing the rest of the restock.
                entry.Amount = Math.Min(entry.Amount + amount, 3 * restock);
            else
                inventory.Add(id, new VendingMachineInventoryEntry(type, id, restock));
        }
    }

}

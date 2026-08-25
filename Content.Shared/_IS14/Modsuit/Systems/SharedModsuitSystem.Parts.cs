// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Interaction.Components;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Deploying and retracting suit parts. Parts move between the control unit's
///     container and the wearer's inventory slots; while worn they are unremovable,
///     so the suit comes off as a unit rather than piece by piece.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private void InitializeParts()
    {
        SubscribeLocalEvent<ModsuitPartComponent, ComponentInit>(OnPartInit);
    }

    private void OnPartInit(Entity<ModsuitPartComponent> ent, ref ComponentInit args)
    {
        ent.Comp.OverslotContainer =
            _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.OverslotContainerId);

        // Fresh plating starts intact. Safe to do here on both sides: the networked
        // value lands after init and overwrites this on the client.
        ent.Comp.Integrity = ent.Comp.MaxIntegrity;
    }

    /// <summary>
    ///     The stash for whatever this part displaced, resolved rather than trusted —
    ///     on the client a container state can arrive before ComponentInit has run.
    /// </summary>
    private bool TryGetOverslotContainer(
        Entity<ModsuitPartComponent> ent,
        [NotNullWhen(true)] out ContainerSlot? container)
    {
        if (ent.Comp.OverslotContainer != null)
        {
            container = ent.Comp.OverslotContainer;
            return true;
        }

        if (_container.TryGetContainer(ent, ent.Comp.OverslotContainerId, out var found)
            && found is ContainerSlot slot)
        {
            ent.Comp.OverslotContainer = slot;
            container = slot;
            return true;
        }

        container = null;
        return false;
    }

    public bool IsDeployed(EntityUid part)
    {
        return TryComp<ModsuitPartComponent>(part, out var comp) && comp.Deployed;
    }

    public bool AnyPartDeployed(Entity<ModsuitControlComponent> ent)
    {
        foreach (var part in ent.Comp.Parts.Values)
        {
            if (IsDeployed(part))
                return true;
        }

        return false;
    }

    public bool AllPartsDeployed(Entity<ModsuitControlComponent> ent)
    {
        foreach (var part in ent.Comp.Parts.Values)
        {
            if (!IsDeployed(part))
                return false;
        }

        return ent.Comp.Parts.Count > 0;
    }

    /// <summary>
    ///     Puts one part onto the wearer, stowing whatever was in that slot so it can be
    ///     handed back when the part retracts.
    /// </summary>
    public bool TryDeployPart(Entity<ModsuitControlComponent> ent, EntityUid part, EntityUid? user = null, bool silent = false)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return false;

        if (!TryComp<ModsuitPartComponent>(part, out var partComp) || partComp.Deployed)
            return false;

        if (ent.Comp.Sealing)
        {
            if (!silent && user != null)
                PopupFail(ent, user.Value, "modsuit-busy-sealing");

            return false;
        }

        var partEnt = new Entity<ModsuitPartComponent>(part, partComp);

        // Make room first. A player wearing gloves should not have to strip before
        // closing their suit — the suit takes the slot over and gives it back later.
        if (!TryDisplaceExisting(partEnt, ent, wearer, user, silent))
            return false;

        // Take it out of storage so the inventory system can accept it.
        if (!TryGetPartContainer(ent, out var container) || !_container.Remove(part, container))
        {
            RestoreDisplaced(partEnt, wearer);
            return false;
        }

        if (!_inventory.TryEquip(wearer, part, partComp.Slot, silent: true, force: false, predicted: true))
        {
            // Slot still refused us; put everything back rather than leaving a mess.
            _container.Insert(part, container);
            RestoreDisplaced(partEnt, wearer);

            if (!silent && user != null)
                PopupFail(ent, user.Value, "modsuit-slot-occupied");

            return false;
        }

        EnsureComp<UnremoveableComponent>(part).DeleteOnDrop = false;

        partComp.Deployed = true;
        Dirty(part, partComp);

        if (!silent)
            _audio.PlayPredicted(ent.Comp.DeploySound, ent, user);

        var ev = new ModsuitPartDeployedEvent(ent, true);
        RaiseLocalEvent(part, ref ev);

        RefreshChassis(ent);
        return true;
    }

    /// <summary>
    ///     Moves whatever occupies the target slot into the part's own stash.
    ///     Returns false only when the slot is blocked by something we must not move.
    /// </summary>
    private bool TryDisplaceExisting(
        Entity<ModsuitPartComponent> part,
        Entity<ModsuitControlComponent> control,
        EntityUid wearer,
        EntityUid? user,
        bool silent)
    {
        if (!_inventory.TryGetSlotEntity(wearer, part.Comp.Slot, out var existing))
            return true;

        if (!part.Comp.CanOverslot || !TryGetOverslotContainer(part, out var container))
            return false;

        // Something already stashed means a previous deploy did not clean up. Refuse
        // rather than overwrite and lose the player's belongings.
        if (container.ContainedEntity != null)
            return false;

        // Nodrop gear — handcuffs, cursed items — is not ours to take off.
        if (HasComp<UnremoveableComponent>(existing.Value))
        {
            if (!silent && user != null)
                PopupFail(control, user.Value, "modsuit-slot-occupied");

            return false;
        }

        if (!_inventory.TryUnequip(wearer, part.Comp.Slot, force: true, predicted: true))
            return false;

        if (!_container.Insert(existing.Value, container))
        {
            // Could not stash it, so put it straight back on rather than dropping it.
            _inventory.TryEquip(wearer, existing.Value, part.Comp.Slot, silent: true, force: true, predicted: true);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Hands back whatever this part displaced, dropping it at the wearer's feet
    ///     if the slot has somehow been taken again.
    /// </summary>
    private void RestoreDisplaced(Entity<ModsuitPartComponent> part, EntityUid wearer)
    {
        if (!TryGetOverslotContainer(part, out var container)
            || container.ContainedEntity is not { } stashed)
            return;

        if (!_container.Remove(stashed, container))
            return;

        if (!_inventory.TryEquip(wearer, stashed, part.Comp.Slot, silent: true, force: true, predicted: true))
            _transform.DropNextTo(stashed, wearer);
    }

    /// <summary>
    ///     Folds one part back into the suit. A sealed part is unsealed first, and
    ///     anything it displaced is handed back.
    /// </summary>
    public bool TryRetractPart(Entity<ModsuitControlComponent> ent, EntityUid part, EntityUid? user = null, bool silent = false)
    {
        if (!TryComp<ModsuitPartComponent>(part, out var partComp) || !partComp.Deployed)
            return false;

        if (ent.Comp.Sealing)
        {
            if (!silent && user != null)
                PopupFail(ent, user.Value, "modsuit-busy-sealing");

            return false;
        }

        var partEnt = new Entity<ModsuitPartComponent>(part, partComp);

        if (partComp.Sealed)
            SetPartSealed(ent, partEnt, false);

        RemComp<UnremoveableComponent>(part);

        if (ent.Comp.Wearer is { } wearer)
            _inventory.TryUnequip(wearer, partComp.Slot, force: true, predicted: true);

        if (!TryGetPartContainer(ent, out var container) || !_container.Insert(part, container))
        {
            Log.Warning($"Could not fold {ToPrettyString(part)} back into {ToPrettyString(ent)}.");
            return false;
        }

        partComp.Deployed = false;
        Dirty(part, partComp);

        // Give the player their own gear back now the slot is free again.
        if (ent.Comp.Wearer is { } stillWorn)
            RestoreDisplaced(partEnt, stillWorn);

        if (!silent)
            _audio.PlayPredicted(ent.Comp.RetractSound, ent, user);

        var ev = new ModsuitPartDeployedEvent(ent, false);
        RaiseLocalEvent(part, ref ev);

        RefreshChassis(ent);
        return true;
    }

    /// <summary>
    ///     Deploys every folded part.
    /// </summary>
    public void DeployAll(Entity<ModsuitControlComponent> ent, EntityUid? user = null, bool silent = false)
    {
        foreach (var part in ent.Comp.Parts.Values)
        {
            TryDeployPart(ent, part, user, silent: true);
        }

        if (!silent)
            _audio.PlayPredicted(ent.Comp.DeploySound, ent, user);
    }

    /// <summary>
    ///     Retracts every deployed part.
    /// </summary>
    public void RetractAll(Entity<ModsuitControlComponent> ent, EntityUid? user = null, bool silent = false)
    {
        foreach (var part in ent.Comp.Parts.Values)
        {
            TryRetractPart(ent, part, user, silent: true);
        }

        if (!silent)
            _audio.PlayPredicted(ent.Comp.RetractSound, ent, user);
    }

    /// <summary>
    ///     Deploys everything if anything is folded, otherwise retracts everything.
    /// </summary>
    public void ToggleDeployAll(Entity<ModsuitControlComponent> ent, EntityUid? user = null)
    {
        if (AllPartsDeployed(ent))
            RetractAll(ent, user);
        else
            DeployAll(ent, user);
    }

    private void RefreshChassis(Entity<ModsuitControlComponent> ent)
    {
        if (TryComp<ModularChassisComponent>(ent, out var chassis))
            _chassis.RefreshModules((ent, chassis));

        // A suit with nothing sealed is a suit that is off. Without this a player who
        // folded the parts away straight from a sealed suit kept paying the base draw
        // for a costume that was back in its case.
        SetActive(ent, IsAnyPartSealed(ent));

        UpdateSealActionState(ent);
        UpdateUi(ent);
    }

    private void PopupFail(Entity<ModsuitControlComponent> ent, EntityUid user, string locId)
    {
        _popup.PopupClient(Loc.GetString(locId), ent, user);
        _audio.PlayPredicted(ent.Comp.FailSound, ent, user);
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Silicons.Laws;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.Cyborgs.Systems;

public abstract partial class SharedCyborgSystem
{
    private void InitializeUpgrades()
    {
        SubscribeLocalEvent<CyborgComponent, SiliconEmaggedEvent>(OnEmagged);
    }

    private void OnEmagged(Entity<CyborgComponent> ent, ref SiliconEmaggedEvent args)
    {
        UnlockEmagTools(ent);
    }

    public void UnlockEmagTools(Entity<CyborgComponent> ent)
    {
        if (ent.Comp.EmagToolsUnlocked)
            return;

        ent.Comp.EmagToolsUnlocked = true;
        Dirty(ent);
    }

    /// <summary>
    /// Installs an upgrade board, used by whoever holds it on the cyborg.
    /// </summary>
    public bool TryInstallUpgrade(Entity<CyborgComponent> ent, Entity<CyborgUpgradeComponent> upgrade, EntityUid user)
    {
        if (TryComp<WiresPanelComponent>(ent, out var panel) && !panel.Open)
        {
            _popup.PopupClient(Loc.GetString("borg-panel-not-open"), ent, user);
            return false;
        }

        if (!UpgradeFits(ent, upgrade))
        {
            _popup.PopupClient(Loc.GetString("cyborg-upgrade-wrong-model"), ent, user);
            return false;
        }

        var proto = Prototype(upgrade)?.ID;
        if (!upgrade.Comp.ResetModel && proto != null &&
            ent.Comp.UpgradeContainer.ContainedEntities.Any(e => Prototype(e)?.ID == proto))
        {
            _popup.PopupClient(Loc.GetString("cyborg-upgrade-duplicate"), ent, user);
            return false;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} installed upgrade {ToPrettyString(upgrade.Owner)} into cyborg {ToPrettyString(ent.Owner)}");
        _popup.PopupClient(Loc.GetString("cyborg-upgrade-installed", ("upgrade", Name(upgrade))), ent, user);

        // Spawning tools and resetting the model is server only, the client stops here.
        if (_net.IsClient)
            return true;

        if (upgrade.Comp.ResetModel)
        {
            QueueDel(upgrade);
            ResetModel(ent);
            return true;
        }

        _container.Insert(upgrade.Owner, ent.Comp.UpgradeContainer);
        ApplyUpgrade(ent, upgrade);
        return true;
    }

    private bool UpgradeFits(Entity<CyborgComponent> ent, Entity<CyborgUpgradeComponent> upgrade)
    {
        return upgrade.Comp.Models.Count == 0 ||
               ent.Comp.Model is { } model && upgrade.Comp.Models.Contains(model);
    }

    private void ApplyUpgrade(Entity<CyborgComponent> ent, Entity<CyborgUpgradeComponent> upgrade)
    {
        foreach (var tool in upgrade.Comp.Tools)
        {
            SpawnTool(ent, tool, emagTool: false, upgrade.Owner);
        }

        EntityManager.AddComponents(ent.Owner, upgrade.Comp.AddComponents);

        if (upgrade.Comp.UnlockEmagTools)
            UnlockEmagTools(ent);
    }

    /// <summary>
    /// After a model change: boards for the old model are lost, the others give their tools again.
    /// </summary>
    private void ReapplyUpgrades(Entity<CyborgComponent> ent)
    {
        foreach (var board in new List<EntityUid>(ent.Comp.UpgradeContainer.ContainedEntities))
        {
            if (!TryComp<CyborgUpgradeComponent>(board, out var upgrade))
                continue;

            if (!UpgradeFits(ent, (board, upgrade)))
            {
                EntityManager.RemoveComponents(ent.Owner, upgrade.AddComponents);
                Del(board);
                continue;
            }

            foreach (var tool in upgrade.Tools)
            {
                SpawnTool(ent, tool, emagTool: false, board);
            }

            // The old model may have taken a shared component away with it.
            EntityManager.AddComponents(ent.Owner, upgrade.AddComponents);
        }
    }

    /// <summary>
    /// Takes an installed board out, like prying it out with a crowbar in tgstation.
    /// Its tools and traits go with it. Server only.
    /// </summary>
    public bool TryRemoveUpgrade(Entity<CyborgComponent> ent, EntityUid board, EntityUid? user)
    {
        if (_net.IsClient || !ent.Comp.UpgradeContainer.Contains(board) ||
            !TryComp<CyborgUpgradeComponent>(board, out var upgrade))
            return false;

        foreach (var tool in EnumerateAllTools(ent).ToList())
        {
            if (CompOrNull<CyborgToolComponent>(tool)?.SourceUpgrade != board)
                continue;

            StoreTool(ent, tool);
            Del(tool);
        }

        _container.Remove(board, ent.Comp.UpgradeContainer);
        EntityManager.RemoveComponents(ent.Owner, upgrade.AddComponents);
        ReapplyComponents(ent);

        // Only the board unlocked them if the cyborg was never emagged for real.
        if (upgrade.UnlockEmagTools && !_emag.CheckFlag(ent.Owner, EmagType.Interaction) &&
            !ent.Comp.UpgradeContainer.ContainedEntities.Any(b => CompOrNull<CyborgUpgradeComponent>(b)?.UnlockEmagTools == true))
        {
            ent.Comp.EmagToolsUnlocked = false;
            StoreAllTools(ent);
            Dirty(ent);
        }

        if (user != null)
        {
            _hands.PickupOrDrop(user.Value, board);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user.Value):player} removed upgrade {ToPrettyString(board)} from cyborg {ToPrettyString(ent.Owner)}");
        }

        return true;
    }

    // Removing one source of a component must not strip it from the model or another board that also gives it.
    private void ReapplyComponents(Entity<CyborgComponent> ent)
    {
        if (_proto.TryIndex(ent.Comp.Model, out var model))
            EntityManager.AddComponents(ent.Owner, model.AddComponents, removeExisting: false);

        foreach (var board in ent.Comp.UpgradeContainer.ContainedEntities)
        {
            if (TryComp<CyborgUpgradeComponent>(board, out var other))
                EntityManager.AddComponents(ent.Owner, other.AddComponents, removeExisting: false);
        }
    }

    /// <summary>
    /// Every tool of the cyborg, stored or held, emag tools included.
    /// </summary>
    private IEnumerable<EntityUid> EnumerateAllTools(Entity<CyborgComponent> ent)
    {
        foreach (var tool in ent.Comp.ToolContainer.ContainedEntities)
        {
            yield return tool;
        }

        for (var i = 0; i < ent.Comp.ToolSlots; i++)
        {
            if (_hands.TryGetHeldItem(ent.Owner, GetToolHandId(ent.Comp, i), out var held) &&
                HasComp<CyborgToolComponent>(held))
                yield return held.Value;
        }
    }
}

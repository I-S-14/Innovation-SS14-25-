// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     The wearer's controls: deploy, seal, and the module menu.
///     Actions appear when the suit goes on and disappear when it comes off.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private void InitializeActions()
    {
        SubscribeLocalEvent<ModsuitActionsComponent, GotEquippedEvent>(OnActionsEquipped);
        SubscribeLocalEvent<ModsuitActionsComponent, GotUnequippedEvent>(OnActionsUnequipped);

        SubscribeLocalEvent<ModsuitControlComponent, ModsuitToggleDeployEvent>(OnToggleDeployAction);
        SubscribeLocalEvent<ModsuitControlComponent, ModsuitToggleSealEvent>(OnToggleSealAction);
        SubscribeLocalEvent<ModsuitControlComponent, ModsuitOpenModulesEvent>(OnOpenModulesAction);
    }

    private void OnActionsEquipped(Entity<ModsuitActionsComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<ModsuitControlComponent>(ent, out var control)
            || (args.SlotFlags & control.RequiredSlot) == 0)
            return;

        _actions.AddAction(args.Equipee, ref ent.Comp.DeployActionEntity, ent.Comp.DeployAction, ent);
        _actions.AddAction(args.Equipee, ref ent.Comp.SealActionEntity, ent.Comp.SealAction, ent);
        _actions.AddAction(args.Equipee, ref ent.Comp.ModulesActionEntity, ent.Comp.ModulesAction, ent);

        Dirty(ent);
    }

    private void OnActionsUnequipped(Entity<ModsuitActionsComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<ModsuitControlComponent>(ent, out var control)
            || (args.SlotFlags & control.RequiredSlot) == 0)
            return;

        // The single-argument overload takes the action off whoever actually holds it,
        // which is the safe call when the suit is being destroyed and the action
        // entities may already be on their way out.
        RemoveActionSafe(ent.Comp.DeployActionEntity);
        RemoveActionSafe(ent.Comp.SealActionEntity);
        RemoveActionSafe(ent.Comp.ModulesActionEntity);
    }

    private void RemoveActionSafe(EntityUid? action)
    {
        if (action is not { } uid || TerminatingOrDeleted(uid))
            return;

        _actions.RemoveAction(uid);
    }

    private void OnToggleDeployAction(Entity<ModsuitControlComponent> ent, ref ModsuitToggleDeployEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanCommandDeploy(ent, args.Performer))
            return;

        ToggleDeployAll(ent, args.Performer);
    }

    private void OnToggleSealAction(Entity<ModsuitControlComponent> ent, ref ModsuitToggleSealEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanCommandSeal(ent, args.Performer))
            return;

        TryToggleSeal(ent, args.Performer);
    }

    /// <summary>
    ///     Whether deploy and fold commands still reach the plating. The actuators are not
    ///     broken, the line to them is: a suit with this wire cut is stuck in whatever
    ///     shape it was in, and the button reports why rather than doing nothing.
    /// </summary>
    private bool CanCommandDeploy(Entity<ModsuitControlComponent> ent, EntityUid? user)
    {
        if (!_lock.IsDeployCut(ent))
            return true;

        if (user is { } uid)
            PopupFail(ent, uid, "modsuit-link-deploy-cut");

        return false;
    }

    /// <summary>
    ///     The same, for pressure. Worth knowing which of the two is gone: one traps you
    ///     in the shape you are in, the other in the pressure you are in.
    /// </summary>
    private bool CanCommandSeal(Entity<ModsuitControlComponent> ent, EntityUid? user)
    {
        if (!_lock.IsSealCut(ent))
            return true;

        if (user is { } uid)
            PopupFail(ent, uid, "modsuit-link-seal-cut");

        return false;
    }

    private void OnOpenModulesAction(Entity<ModsuitControlComponent> ent, ref ModsuitOpenModulesEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // The action opens the readout with the raw engine call, which is the one route
        // into it that never raises ActivatableUI's attempt event — so every refusal that
        // hangs off that event has to be repeated here or the button walks straight past
        // it. That is exactly how a wrecked interface stayed openable.
        if (!CanUseInterface(ent, args.Performer))
            return;

        _ui.TryToggleUi(ent.Owner, ModularChassisUiKey.Key, args.Performer);
    }

    /// <summary>
    ///     Whether the suit's own readout will answer this person at all. A wrecked
    ///     interface answers nobody, the wearer included: being stuck with the
    ///     configuration you had is the whole point of the wire.
    /// </summary>
    public bool CanUseInterface(Entity<ModsuitControlComponent> ent, EntityUid? user)
    {
        if (!_lock.IsInterfaceBroken(ent))
            return true;

        if (user is { } uid)
            PopupFail(ent, uid, "modsuit-interface-broken");

        return false;
    }

    /// <summary>
    ///     Keeps the seal action's toggle state in step with the suit, so the button
    ///     reads as "unseal" once the suit is closed up.
    /// </summary>
    private void UpdateSealActionState(Entity<ModsuitControlComponent> ent)
    {
        if (!TryComp<ModsuitActionsComponent>(ent, out var actions))
            return;

        _actions.SetToggled(actions.SealActionEntity, IsAnyPartSealed(ent));
        _actions.SetToggled(actions.DeployActionEntity, AnyPartDeployed(ent));
    }
}

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
        ToggleDeployAll(ent, args.Performer);
    }

    private void OnToggleSealAction(Entity<ModsuitControlComponent> ent, ref ModsuitToggleSealEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryToggleSeal(ent, args.Performer);
    }

    private void OnOpenModulesAction(Entity<ModsuitControlComponent> ent, ref ModsuitOpenModulesEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.TryToggleUi(ent.Owner, ModularChassisUiKey.Key, args.Performer);
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

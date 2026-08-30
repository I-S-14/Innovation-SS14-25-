// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;
using Robust.Shared.Utility;

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
        SubscribeLocalEvent<ModsuitControlComponent, ModsuitOpenPanelEvent>(OnOpenPanelAction);
    }

    private void OnActionsEquipped(Entity<ModsuitActionsComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<ModsuitControlComponent>(ent, out var control)
            || (args.SlotFlags & control.RequiredSlot) == 0)
            return;

        _actions.AddAction(args.Equipee, ref ent.Comp.DeployActionEntity, ent.Comp.DeployAction, ent);
        _actions.AddAction(args.Equipee, ref ent.Comp.SealActionEntity, ent.Comp.SealAction, ent);
        _actions.AddAction(args.Equipee, ref ent.Comp.ModulesActionEntity, ent.Comp.ModulesAction, ent);
        _actions.AddAction(args.Equipee, ref ent.Comp.PanelActionEntity, ent.Comp.PanelAction, ent);

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
        RemoveActionSafe(ent.Comp.PanelActionEntity);
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

        // A suit with no arming component behaves the old way rather than becoming
        // unusable, which matters for anything that reuses the control without actions.
        if (!TryComp<ModsuitActionsComponent>(ent, out var actions))
        {
            TryToggleSeal(ent, args.Performer);
            return;
        }

        var now = _timing.CurTime;

        if (actions.SealArmedUntil is not { } until || now >= until)
        {
            actions.SealArmedUntil = now + actions.SealArmWindow;
            Dirty(ent.Owner, actions);
            UpdateSealActionState(ent);

            _popup.PopupClient(
                Loc.GetString(IsAnyPartSealed(ent) ? "modsuit-unseal-confirm" : "modsuit-seal-confirm"),
                ent,
                args.Performer);

            return;
        }

        actions.SealArmedUntil = null;
        Dirty(ent.Owner, actions);

        TryToggleSeal(ent, args.Performer);
        UpdateSealActionState(ent);
    }

    /// <summary>
    ///     Drops the arming once its window passes, so the button is never found still
    ///     lit from a press made minutes ago — which would defeat the whole point of it.
    /// </summary>
    private void ExpireSealArming(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ModsuitActionsComponent, ModsuitControlComponent>();

        while (query.MoveNext(out var uid, out var actions, out var control))
        {
            if (actions.SealArmedUntil is not { } until || now < until)
                continue;

            actions.SealArmedUntil = null;
            Dirty(uid, actions);
            UpdateSealActionState((uid, control));
        }
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

        // The action opens the interface with the raw engine call, which is the one route
        // into it that never raises ActivatableUI's attempt event — so every refusal that
        // hangs off that event has to be repeated here or the button walks straight past
        // it. That is exactly how a wrecked interface stayed openable.
        if (!CanUseInterface(ent, args.Performer))
            return;

        // An empty ring is worse than no ring: it opens, says nothing, and has to be
        // dismissed. Passive modules do not count — they have no switch to offer.
        if (!HasSwitchableModule(ent))
        {
            PopupFail(ent, args.Performer, "modsuit-no-switchable-modules");
            return;
        }

        // The ring, not the readout: this is the button pressed mid-fight to put the
        // lamp on. Reading the suit is a different job with its own button.
        _ui.TryToggleUi(ent.Owner, ModularChassisUiKey.Radial, args.Performer);
    }

    /// <summary>
    ///     Whether anything installed can actually be switched, used or selected. Mirrors
    ///     what the ring itself draws, so the two never disagree about being empty.
    /// </summary>
    private bool HasSwitchableModule(Entity<ModsuitControlComponent> ent)
    {
        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return false;

        foreach (var module in _chassis.GetModules((ent.Owner, chassis)))
        {
            if (module.Comp.Kind != ModuleKind.Passive)
                return true;
        }

        return false;
    }

    private void OnOpenPanelAction(Entity<ModsuitControlComponent> ent, ref ModsuitOpenPanelEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

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

    private static readonly ResPath ActionIcons = new("_IS14/Interface/Actions/modsuit.rsi");

    private static readonly SpriteSpecifier SealIcon = new SpriteSpecifier.Rsi(ActionIcons, "activate");
    private static readonly SpriteSpecifier SealArmedIcon = new SpriteSpecifier.Rsi(ActionIcons, "activate-ready");
    private static readonly SpriteSpecifier UnsealIcon = new SpriteSpecifier.Rsi(ActionIcons, "unseal");
    private static readonly SpriteSpecifier UnsealArmedIcon = new SpriteSpecifier.Rsi(ActionIcons, "unseal-ready");

    /// <summary>
    ///     Keeps the buttons in step with the suit.
    ///
    ///     The seal button carries two separate facts, so it uses two channels: the icon
    ///     says which way it will go — blue to close up, red to open — and the toggle says
    ///     whether it is armed and waiting for the second press.
    /// </summary>
    private void UpdateSealActionState(Entity<ModsuitControlComponent> ent)
    {
        if (!TryComp<ModsuitActionsComponent>(ent, out var actions))
            return;

        var willUnseal = IsAnyPartSealed(ent);

        // Both icons move together: the armed state has to stay the same colour as the
        // idle one, or arming a breach would light up in the "closing up" blue.
        if (actions.SealActionEntity is { } seal)
        {
            _actions.SetIcon(seal, willUnseal ? UnsealIcon : SealIcon);
            _actions.SetIconOn(seal, willUnseal ? UnsealArmedIcon : SealArmedIcon);
        }

        _actions.SetToggled(actions.SealActionEntity, actions.SealArmedUntil != null);
        _actions.SetToggled(actions.DeployActionEntity, AnyPartDeployed(ent));
    }
}

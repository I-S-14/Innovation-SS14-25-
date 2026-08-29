// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modular.Systems;

/// <summary>
///     Getting at the hardware: screwdriver opens the panel, a module clicked on an open
///     chassis goes in, a prying tool levers the power source out. Modules come back out
///     through the interface, where the panel state is already on screen.
/// </summary>
public sealed partial class SharedModularChassisSystem
{
    // [ForbidLiteral] on the tool APIs means these have to be named, not inlined.
    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    private const float PanelToggleDelay = 1f;
    private const float ModuleInstallDelay = 1f;
    private const float PryDelay = 2f;

    private void InitializeInteraction()
    {
        SubscribeLocalEvent<ModularChassisComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<ModularChassisComponent, ChassisTogglePanelDoAfterEvent>(OnTogglePanelDoAfter);
        SubscribeLocalEvent<ModularChassisComponent, ChassisInstallModuleDoAfterEvent>(OnInstallDoAfter);
        SubscribeLocalEvent<ModularChassisComponent, ChassisPryDoAfterEvent>(OnPryDoAfter);
        SubscribeLocalEvent<ModularChassisComponent, PanelChangedEvent>(OnPanelChanged);
    }

    private void OnInteractUsing(Entity<ModularChassisComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Screwdriver: the panel. Refused while the chassis is running, so nobody opens
        // a live suit up in the middle of a spacewalk. A chassis with a real wires panel
        // lets WiresSystem own the screwdriver instead — see OnPanelChanged.
        if (_tool.HasQuality(args.Used, ScrewingQuality) && !HasComp<WiresPanelComponent>(ent))
        {
            if (ent.Comp.Active)
            {
                _popup.PopupClient(Loc.GetString("chassis-active-cannot-open"), ent, args.User);
                _audio.PlayPredicted(ent.Comp.FailSound, ent, args.User);
                args.Handled = true;
                return;
            }

            args.Handled = _tool.UseTool(
                args.Used,
                args.User,
                ent,
                PanelToggleDelay,
                [ScrewingQuality],
                new ChassisTogglePanelDoAfterEvent());
            return;
        }

        // Prying tool: lever the power source out of its cradle. Deliberately not a way
        // to fish modules out — those have their own button in the panel readout, and a
        // crowbar that emptied the whole bay one blind pull at a time was never the point.
        if (_tool.HasQuality(args.Used, PryingQuality))
        {
            // A panel plated over is not a panel yet. The crowbar's job there is the
            // plating, which the construction graph owns, so the click is left unhandled
            // for it rather than answered with "nothing to pry".
            if (TryComp<WiresPanelSecurityComponent>(ent, out var security) && !security.WiresAccessible)
                return;

            if (!RequirePanelOpen(ent, args.User))
            {
                args.Handled = true;
                return;
            }

            var check = new ChassisPryEvent(args.User, true, false);
            RaiseLocalEvent(ent, ref check);

            if (!check.Handled)
            {
                _popup.PopupClient(Loc.GetString("chassis-nothing-to-pry"), ent, args.User);
                _audio.PlayPredicted(ent.Comp.FailSound, ent, args.User);
                args.Handled = true;
                return;
            }

            args.Handled = _tool.UseTool(
                args.Used,
                args.User,
                ent,
                PryDelay,
                [PryingQuality],
                new ChassisPryDoAfterEvent());
            return;
        }

        // A module clicked on an open chassis goes in.
        if (TryComp<ChassisModuleComponent>(args.Used, out var moduleComp))
        {
            if (!RequirePanelOpen(ent, args.User))
            {
                args.Handled = true;
                return;
            }

            if (!CanInstall(ent, (args.Used, moduleComp), out var reason))
            {
                _popup.PopupClient(reason, ent, args.User);
                _audio.PlayPredicted(ent.Comp.FailSound, ent, args.User);
                args.Handled = true;
                return;
            }

            args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager,
                args.User,
                TimeSpan.FromSeconds(ModuleInstallDelay),
                new ChassisInstallModuleDoAfterEvent(),
                ent,
                ent,
                args.Used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            });
        }
    }

    private bool RequirePanelOpen(Entity<ModularChassisComponent> ent, EntityUid user)
    {
        if (ent.Comp.PanelOpen)
            return true;

        _popup.PopupClient(Loc.GetString("chassis-panel-closed"), ent, user);
        _audio.PlayPredicted(ent.Comp.FailSound, ent, user);
        return false;
    }

    /// <summary>
    ///     Mirrors the wires panel into the chassis' own flag, so a suit with wires and
    ///     a mech without both answer "is the hardware exposed?" the same way.
    /// </summary>
    private void OnPanelChanged(Entity<ModularChassisComponent> ent, ref PanelChangedEvent args)
    {
        SetPanelOpen(ent, args.Open);
    }

    private void OnTogglePanelDoAfter(Entity<ModularChassisComponent> ent, ref ChassisTogglePanelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Active)
            return;

        args.Handled = true;
        SetPanelOpen(ent, !ent.Comp.PanelOpen);

        _popup.PopupClient(
            Loc.GetString(ent.Comp.PanelOpen ? "chassis-panel-opened" : "chassis-panel-closed-now"),
            ent,
            args.User);
    }

    private void OnInstallDoAfter(Entity<ModularChassisComponent> ent, ref ChassisInstallModuleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used == null)
            return;

        if (!TryComp<ChassisModuleComponent>(args.Used.Value, out var module))
            return;

        args.Handled = true;
        TryInstall(ent, (args.Used.Value, module), args.User);
    }

    private void OnPryDoAfter(Entity<ModularChassisComponent> ent, ref ChassisPryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var pry = new ChassisPryEvent(args.User, false, false);
        RaiseLocalEvent(ent, ref pry);

        if (!pry.Handled)
            _popup.PopupClient(Loc.GetString("chassis-nothing-to-pry"), ent, args.User);
    }
}

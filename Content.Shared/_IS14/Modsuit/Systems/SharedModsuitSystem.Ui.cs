// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular.Behaviours;
using Content.Shared.Atmos.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Atmos;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using System.Linq;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Feeds the chassis UI and acts on what the player clicks in it.
///     Lives on the modsuit side because it is the only layer that knows about parts
///     and wearers; the window itself is generic and will serve mechs unchanged.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private void InitializeUi()
    {
        SubscribeLocalEvent<ModsuitControlComponent, ChassisSelectModuleMessage>(OnSelectModuleMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisConfigureModuleMessage>(OnConfigureModuleMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisEjectModuleMessage>(OnEjectModuleMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisEjectCellMessage>(OnEjectCellMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisInsertCellMessage>(OnInsertCellMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisOpenHopperMessage>(OnOpenHopperMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisTogglePartMessage>(OnTogglePartMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisSealPartMessage>(OnSealPartMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisToggleActiveMessage>(OnToggleActiveMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisToggleDeployMessage>(OnToggleDeployMessage);

        // Anything that changes what the window shows pushes a fresh state.
        SubscribeLocalEvent<ModsuitControlComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisModulesChangedEvent>(OnChassisChanged);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisPowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisStateChangedEvent>(OnChassisStateChanged);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisPanelChangedEvent>(OnPanelChanged);
        SubscribeLocalEvent<ModsuitControlComponent, ModsuitSecurityChangedEvent>(OnSecurityChanged);
    }

    private void OnUiOpened(Entity<ModsuitControlComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnChassisChanged(Entity<ModsuitControlComponent> ent, ref ChassisModulesChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSecurityChanged(Entity<ModsuitControlComponent> ent, ref ModsuitSecurityChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPowerChanged(Entity<ModsuitControlComponent> ent, ref ChassisPowerChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPanelChanged(Entity<ModsuitControlComponent> ent, ref ChassisPanelChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnChassisStateChanged(Entity<ModsuitControlComponent> ent, ref ChassisStateChangedEvent args)
    {
        _visuals.SetSealedLook(ent, args.Active);
        UpdateUi(ent);
    }

    private void OnSelectModuleMessage(Entity<ModsuitControlComponent> ent, ref ChassisSelectModuleMessage args)
    {
        var module = GetEntity(args.Module);

        if (!TryComp<ChassisModuleComponent>(module, out var comp) || comp.Chassis != ent.Owner)
            return;

        _modules.TrySelect((module, comp), args.Actor);
        UpdateUi(ent);
    }

    private void OnConfigureModuleMessage(Entity<ModsuitControlComponent> ent, ref ChassisConfigureModuleMessage args)
    {
        var module = GetEntity(args.Module);

        if (!TryComp<ChassisModuleComponent>(module, out var comp) || comp.Chassis != ent.Owner)
            return;

        var ev = new ModuleConfigChangedEvent(args.Key, args.Value, false);
        RaiseLocalEvent(module, ref ev);

        UpdateUi(ent);
    }

    /// <summary>
    ///     Pulling a module is done from the interface rather than the context menu: the
    ///     panel is where the hardware lives, so that is where the button belongs.
    /// </summary>
    private void OnEjectModuleMessage(Entity<ModsuitControlComponent> ent, ref ChassisEjectModuleMessage args)
    {
        var module = GetEntity(args.Module);

        if (!TryComp<ChassisModuleComponent>(module, out var comp) || comp.Chassis != ent.Owner)
            return;

        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return;

        if (!chassis.PanelOpen)
        {
            PopupFail(ent, args.Actor, "chassis-panel-closed");
            UpdateUi(ent);
            return;
        }

        _chassis.TryUninstall((ent.Owner, chassis), (module, comp), args.Actor);
        UpdateUi(ent);
    }

    /// <summary>
    ///     Takes the cell out of the installed core. Same rule as pulling a module: the
    ///     panel has to be open, because this is the inside of the suit.
    /// </summary>
    private void OnEjectCellMessage(Entity<ModsuitControlComponent> ent, ref ChassisEjectCellMessage args)
    {
        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return;

        if (!chassis.PanelOpen)
        {
            PopupFail(ent, args.Actor, "chassis-panel-closed");
            UpdateUi(ent);
            return;
        }

        if (TryComp<ModCoreSlotComponent>(ent, out var slot) && _core.GetCore((ent.Owner, slot)) is { } core)
            _core.TryEjectCell(core.Owner, args.Actor);

        UpdateUi(ent);
    }

    /// <summary>
    ///     Puts the cell in hand into the core. Same panel rule as taking one out.
    /// </summary>
    private void OnInsertCellMessage(Entity<ModsuitControlComponent> ent, ref ChassisInsertCellMessage args)
    {
        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return;

        if (!chassis.PanelOpen)
        {
            PopupFail(ent, args.Actor, "chassis-panel-closed");
            UpdateUi(ent);
            return;
        }

        if (TryComp<ModCoreSlotComponent>(ent, out var slot) && _core.GetCore((ent.Owner, slot)) is { } core)
            _core.TryInsertCell(core.Owner, args.Actor);

        UpdateUi(ent);
    }

    /// <summary>
    ///     Opens the fuel hopper on the installed core. No panel needed — filling the
    ///     tank is not surgery.
    /// </summary>
    private void OnOpenHopperMessage(Entity<ModsuitControlComponent> ent, ref ChassisOpenHopperMessage args)
    {
        if (!TryComp<ModCoreSlotComponent>(ent, out var slot)
            || _core.GetCore((ent.Owner, slot)) is not { } core
            || !HasComp<StorageComponent>(core))
            return;

        _storage.OpenStorageUI(core, args.Actor, silent: false);
    }

    private void OnTogglePartMessage(Entity<ModsuitControlComponent> ent, ref ChassisTogglePartMessage args)
    {
        var part = GetEntity(args.Part);

        if (!TryComp<ModsuitPartComponent>(part, out var comp) || comp.Control != ent.Owner)
            return;

        if (comp.Deployed)
            TryRetractPart(ent, part, args.Actor);
        else
            TryDeployPart(ent, part, args.Actor);

        UpdateUi(ent);
    }

    private void OnSealPartMessage(Entity<ModsuitControlComponent> ent, ref ChassisSealPartMessage args)
    {
        var part = GetEntity(args.Part);

        if (!TryComp<ModsuitPartComponent>(part, out var comp) || comp.Control != ent.Owner)
            return;

        TrySealPart(ent, part, !comp.Sealed, args.Actor);
        UpdateUi(ent);
    }

    private void OnToggleActiveMessage(Entity<ModsuitControlComponent> ent, ref ChassisToggleActiveMessage args)
    {
        TryToggleSeal(ent, args.Actor);
        UpdateUi(ent);
    }

    private void OnToggleDeployMessage(Entity<ModsuitControlComponent> ent, ref ChassisToggleDeployMessage args)
    {
        ToggleDeployAll(ent, args.Actor);
        UpdateUi(ent);
    }

    /// <summary>
    ///     Rebuilds and pushes the UI state. Cheap enough to call from any state change.
    /// </summary>
    public void UpdateUi(Entity<ModsuitControlComponent> ent)
    {
        // Server only, and not merely as an optimisation. Half of what this state carries
        // is invisible to the client — a gas tank's mixture is not a networked field, for
        // one — so a client rebuilding the state mid-prediction overwrites good numbers
        // with blanks until the next server push. That is what made the bottle read
        // 0 kPa for a moment every time a part sealed.
        if (_net.IsClient)
            return;

        if (!_ui.IsUiOpen(ent.Owner, ModularChassisUiKey.Key))
            return;

        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return;

        var (charge, maxCharge) = _power.GetCharge(ent);

        var state = new ModularChassisUiState
        {
            ChassisName = Name(ent),
            AnyDeployed = AnyPartDeployed(ent),
            Charge = charge,
            MaxCharge = maxCharge,
            CoreName = GetCoreName(ent),
            UsedComplexity = chassis.UsedComplexity,
            MaxComplexity = chassis.MaxComplexity,
            Active = chassis.Active,
            PanelOpen = chassis.PanelOpen,
            Draw = GetDraw((ent.Owner, ent.Comp, chassis)),
            SelectedModule = chassis.SelectedModule is { } sel ? GetNetEntity(sel) : null,
        };

        if (TryComp<ChassisPowerComponent>(ent, out var power))
            state.Malfunctioning = power.Malfunctioning;

        if (TryComp<ModsuitLockComponent>(ent, out var lockComp))
        {
            state.Locked = lockComp.Locked;
            state.AccessWiped = lockComp.AccessWiped;
        }

        if (TryComp<ModsuitSabotageComponent>(ent, out var sabotage))
        {
            state.InterfaceBroken = sabotage.InterfaceBroken;
            state.Electrified = _lock.IsElectrified((ent.Owner, sabotage));
        }

        FillCore(ent, state);
        FillTank(ent, state);
        FillWearer(ent, state);
        FillModules((ent.Owner, ent.Comp, chassis), state);
        FillParts(ent, state);

        _ui.SetUiState(ent.Owner, ModularChassisUiKey.Key, state);
    }

    private float GetDraw(Entity<ModsuitControlComponent, ModularChassisComponent> ent)
    {
        return TryComp<ChassisPowerComponent>(ent, out var power)
            ? _power.GetTotalDraw((ent.Owner, ent.Comp2), power)
            : 0f;
    }

    private string? GetCoreName(Entity<ModsuitControlComponent> ent)
    {
        if (!TryComp<ModCoreSlotComponent>(ent, out var slot))
            return null;

        return _core.GetCore((ent.Owner, slot)) is { } core ? Name(core) : null;
    }

    /// <summary>
    ///     What the installed core offers the panel: a cell you can pull, a hopper you
    ///     can fill, or neither.
    /// </summary>
    private void FillCore(Entity<ModsuitControlComponent> ent, ModularChassisUiState state)
    {
        if (!TryComp<ModCoreSlotComponent>(ent, out var slot)
            || _core.GetCore((ent.Owner, slot)) is not { } core)
            return;

        state.Core = GetNetEntity(core);
        state.CoreHasHopper = HasComp<StorageComponent>(core);
        state.CoreTakesCell = _core.TakesCell(core);

        if (_core.GetCell(core.Owner) is { } cell)
            state.CoreCellName = Name(cell);
    }

    /// <summary>
    ///     The bottle, when an atmospheric module has given the suit one.
    /// </summary>
    private void FillTank(Entity<ModsuitControlComponent> ent, ModularChassisUiState state)
    {
        if (!TryComp<GasTankComponent>(ent, out var tank))
            return;

        state.TankPresent = true;
        state.TankPressure = tank.Air.Pressure;
        state.TankTargetPressure = Atmospherics.OneAtmosphere * 10f;
        state.TankTemperature = tank.Air.Temperature;
        state.TankEmpty = tank.Air.TotalMoles <= 0f;
        state.TankValveOpen = tank.IsValveOpen;
        state.TankInternalsOn = tank.IsConnected;

        // Breathing is the one thing that asks for the whole suit rather than a piece
        // of it: a helmet full of air over an open chestplate is just a hat.
        state.TankCanBreathe = IsFullySealed(ent);

        var total = tank.Air.TotalMoles;

        if (total > 0f)
        {
            foreach (var (gas, moles) in tank.Air)
            {
                if (moles <= 0f)
                    continue;

                state.TankGases.Add(new ChassisGasUiEntry(gas, moles / total));
            }

            state.TankGases.Sort((a, b) => b.Fraction.CompareTo(a.Fraction));
        }

        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return;

        foreach (var (module, moduleComp) in _chassis.GetModules((ent.Owner, chassis)))
        {
            if (!TryComp<ModuleGasTankComponent>(module, out var gas))
                continue;

            state.TankModule = GetNetEntity(module);
            state.TankTargetPressure = gas.TargetPressure;
            state.TankPumpEnabled = gas.Filtering;

            // Enabled is the module's own gate on the sealed chestplate, so the gauge
            // reports the same reason the compressor itself is standing still.
            state.TankCanPump = moduleComp.Enabled;
            state.TankPumping = gas.Filtering && moduleComp.Enabled && gas.Filtered.Count > 0;
            state.TankContents = string.Join(", ", gas.Filtered.Select(
                g => Loc.GetString($"chassis-config-gas-{g.ToString().ToLowerInvariant()}")));

            break;
        }
    }

    private void FillWearer(Entity<ModsuitControlComponent> ent, ModularChassisUiState state)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return;

        state.WearerName = Name(wearer);

        // Read the job off the worn ID rather than the mind, so a stolen suit reports
        // whoever's card is in it — which is the interesting answer, not the true one.
        if (_inventory.TryGetSlotEntity(wearer, "id", out var idSlot)
            && _idCard.TryFindIdCard(idSlot.Value, out var id))
        {
            state.WearerJob = id.Comp.LocalizedJobTitle ?? string.Empty;
        }
    }

    private void FillModules(Entity<ModsuitControlComponent, ModularChassisComponent> ent, ModularChassisUiState state)
    {
        foreach (var (module, comp) in _chassis.GetModules((ent.Owner, ent.Comp2)))
        {
            var config = new List<ModuleConfigEntry>();
            var configEv = new ModuleGetConfigEvent(config);
            RaiseLocalEvent(module, ref configEv);

            _modules.CanUse((module, comp), (ent.Owner, ent.Comp2), ent.Comp1.Wearer, out var blockReason);

            state.Modules.Add(new ChassisModuleUiEntry
            {
                Module = GetNetEntity(module),
                Name = Name(module),
                Description = MetaData(module).EntityDescription,
                Kind = comp.Kind,
                Complexity = comp.Complexity,
                IdleDraw = comp.IdleDraw,
                ActiveDraw = comp.ActiveDraw,
                UseCost = comp.UseCost,
                Active = comp.Active,
                Enabled = comp.Enabled,
                Removable = comp.Removable,
                RequiredSlots = new List<SlotFlags>(comp.RequiredSlots),
                ActionText = comp.ActionText,
                ActionIcon = comp.ActionIcon,
                Cooldown = (float)_modules.GetCooldownRemaining(comp).TotalSeconds,
                CooldownMax = (float)comp.Cooldown.TotalSeconds,
                BlockReason = blockReason,
                Config = config,
            });
        }
    }

    private void FillParts(Entity<ModsuitControlComponent> ent, ModularChassisUiState state)
    {
        foreach (var (slot, part) in ent.Comp.Parts)
        {
            if (!TryComp<ModsuitPartComponent>(part, out var comp))
                continue;

            state.Parts.Add(new ChassisPartUiEntry
            {
                Part = GetNetEntity(part),
                Name = Name(part),
                Slot = slot,
                SlotFlag = comp.SlotFlag,
                Deployed = comp.Deployed,
                Sealed = comp.Sealed,
                Integrity = comp.Integrity,
                MaxIntegrity = comp.MaxIntegrity,
                ModuleThreshold = comp.ModuleThreshold,
                UnsealThreshold = comp.UnsealThreshold,
                Broken = IsPartBroken((part, comp)),
                Ruptured = IsPartRuptured((part, comp)),
                Fault = GetFault((part, comp)),
            });
        }
    }
}

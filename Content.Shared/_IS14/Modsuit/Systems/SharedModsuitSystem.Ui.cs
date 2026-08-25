// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Inventory;

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
        SubscribeLocalEvent<ModsuitControlComponent, ChassisTogglePartMessage>(OnTogglePartMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisSealPartMessage>(OnSealPartMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisToggleActiveMessage>(OnToggleActiveMessage);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisToggleDeployMessage>(OnToggleDeployMessage);

        // Anything that changes what the window shows pushes a fresh state.
        SubscribeLocalEvent<ModsuitControlComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisModulesChangedEvent>(OnChassisChanged);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisPowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ModsuitControlComponent, ChassisStateChangedEvent>(OnChassisStateChanged);
    }

    private void OnUiOpened(Entity<ModsuitControlComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnChassisChanged(Entity<ModsuitControlComponent> ent, ref ChassisModulesChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPowerChanged(Entity<ModsuitControlComponent> ent, ref ChassisPowerChangedEvent args)
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
                BreakThreshold = comp.BreakThreshold,
                Broken = IsPartBroken((part, comp)),
            });
        }
    }
}

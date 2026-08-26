// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Behaviours;
using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Client.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Modsuit;

/// <summary>
///     Paints installed modules onto the suit the wearer is actually wearing.
///
///     This has to live on the client: <c>ClothingComponent.ClothingVisuals</c> is a plain
///     data field with no network state, so the server cannot push these layers. Instead the
///     layers are contributed at draw time from the module state, which *is* networked.
/// </summary>
public sealed class ModsuitModuleOverlaySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedModularChassisSystem _chassis = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitPartComponent, GetEquipmentVisualsEvent>(OnGetPartVisuals, after: [typeof(ClientClothingSystem)]);

        // Anything that changes what a module should look like has to redraw the part
        // it lives on, because the part is what the inventory actually renders.
        SubscribeLocalEvent<ModuleWornOverlayComponent, ModuleActivatedEvent>(OnModuleVisualChanged);
        SubscribeLocalEvent<ModuleWornOverlayComponent, ModuleDeactivatedEvent>(OnModuleVisualChanged);
        SubscribeLocalEvent<ModuleWornOverlayComponent, ModuleEnabledEvent>(OnModuleEnabled);
        SubscribeLocalEvent<ModuleWornOverlayComponent, ModuleDisabledEvent>(OnModuleDisabled);
        SubscribeLocalEvent<ModuleWornOverlayComponent, ModuleInstalledEvent>(OnModuleInstalled);
        SubscribeLocalEvent<ModuleWornOverlayComponent, ModuleUninstalledEvent>(OnModuleUninstalled);

        // The events above only fire on whichever side ran the toggle, and the switch is
        // a server-side interface message — so on the client the module simply changed
        // state underneath us. This is the only notice we get that it did.
        SubscribeLocalEvent<ChassisModuleComponent, AfterAutoHandleStateEvent>(OnModuleState);
    }

    private void OnModuleState(Entity<ChassisModuleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<ModuleWornOverlayComponent>(ent, out var overlay))
            RefreshParts((ent.Owner, overlay));
    }

    private void OnGetPartVisuals(Entity<ModsuitPartComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (ent.Comp.Control is not { } control
            || !TryComp<ModularChassisComponent>(control, out var chassis))
            return;

        if (ent.Comp.SlotFlag == 0 || !ent.Comp.Deployed)
            return;

        var index = 0;

        foreach (var module in _chassis.GetModuleEntities((control, chassis)))
        {
            if (!TryComp<ModuleWornOverlayComponent>(module, out var overlay)
                || (overlay.TargetSlot & ent.Comp.SlotFlag) == 0)
                continue;

            if (overlay.RequireSealed && !ent.Comp.Sealed)
                continue;

            if (GetState(module, overlay) is not { } state)
                continue;

            // Keyed by slot as well as index. The key is what the inventory maps the
            // layer under, and that map is global to the mob sprite — two parts both
            // claiming "modsuit-module-0" means unequipping one leaves the other's layer
            // on the body with no key left to remove it by.
            args.Layers.Add(($"modsuit-module-{ent.Comp.Slot}-{index}", new PrototypeLayerData
            {
                RsiPath = overlay.Rsi.ToString(),
                State = state,
                Shader = overlay.Unshaded ? "unshaded" : null,
                Visible = true,
            }));

            index++;
        }
    }

    /// <summary>
    ///     Cooldown beats on, on beats off, and a module with nothing to show for its
    ///     current state simply does not draw.
    /// </summary>
    private string? GetState(EntityUid module, ModuleWornOverlayComponent overlay)
    {
        if (!TryComp<ChassisModuleComponent>(module, out var comp) || !comp.Enabled)
            return null;

        if (overlay.StateCooldown != null && _timing.CurTime < comp.CooldownEnd)
            return overlay.StateCooldown;

        if (comp.Active)
            return overlay.StateActive ?? overlay.StateInactive;

        return overlay.StateInactive;
    }

    private void OnModuleVisualChanged<T>(Entity<ModuleWornOverlayComponent> ent, ref T args) where T : struct
    {
        RefreshParts(ent);
    }

    private void OnModuleEnabled(Entity<ModuleWornOverlayComponent> ent, ref ModuleEnabledEvent args) => RefreshParts(ent);
    private void OnModuleDisabled(Entity<ModuleWornOverlayComponent> ent, ref ModuleDisabledEvent args) => RefreshParts(ent);
    private void OnModuleInstalled(Entity<ModuleWornOverlayComponent> ent, ref ModuleInstalledEvent args) => RefreshParts(ent);
    private void OnModuleUninstalled(Entity<ModuleWornOverlayComponent> ent, ref ModuleUninstalledEvent args) => RefreshParts(ent);

    /// <summary>
    ///     Redraws every suit part this module could be showing on.
    /// </summary>
    private void RefreshParts(Entity<ModuleWornOverlayComponent> ent)
    {
        if (!TryComp<ChassisModuleComponent>(ent, out var module)
            || module.Chassis is not { } chassis
            || !TryComp<ModsuitControlComponent>(chassis, out var control))
            return;

        foreach (var part in control.Parts.Values)
        {
            if (TryComp<ModsuitPartComponent>(part, out var partComp)
                && partComp.Deployed
                && (ent.Comp.TargetSlot & partComp.SlotFlag) != 0)
            {
                _item.VisualsChanged(part);
            }
        }
    }
}

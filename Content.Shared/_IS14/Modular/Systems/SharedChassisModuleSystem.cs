// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Modular.Systems;

/// <summary>
///     Owns the module side of the system: whether a module may run, switching it on and off,
///     firing one-shot uses, and cooldowns. Behaviour components do the actual work by
///     subscribing to the events this raises.
/// </summary>
public sealed class SharedChassisModuleSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ChassisPowerSystem _power = default!;

    /// <summary>
    ///     Does the chassis currently offer every slot this module needs?
    ///     Each entry in <see cref="ChassisModuleComponent.RequiredSlots"/> must be matched;
    ///     flags combined within one entry are alternatives.
    /// </summary>
    public bool HasRequiredSlots(ChassisModuleComponent module, SlotFlags available)
    {
        foreach (var required in module.RequiredSlots)
        {
            if ((required & available) == SlotFlags.NONE)
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Flips a module between runnable and not. Passive modules follow this directly;
    ///     anything switched on is forced off when it stops being runnable.
    /// </summary>
    public void SetEnabled(Entity<ChassisModuleComponent> module, EntityUid chassis, bool enabled)
    {
        if (module.Comp.Enabled == enabled)
            return;

        module.Comp.Enabled = enabled;

        if (enabled)
        {
            var ev = new ModuleEnabledEvent(chassis);
            RaiseLocalEvent(module, ref ev);

            // A passive module has no separate on/off, so being runnable is being on.
            if (module.Comp.Kind == ModuleKind.Passive)
                module.Comp.Active = true;
        }
        else
        {
            if (module.Comp.Active)
                Deactivate(module, chassis, null, quiet: true);

            var ev = new ModuleDisabledEvent(chassis);
            RaiseLocalEvent(module, ref ev);
        }

        Dirty(module);
    }

    /// <summary>
    ///     Whether the module can be triggered right now, and why not if it cannot.
    /// </summary>
    public bool CanUse(
        Entity<ChassisModuleComponent> module,
        Entity<ModularChassisComponent> chassis,
        EntityUid? user,
        out ModuleBlockReason reason)
    {
        reason = ModuleBlockReason.None;

        if (module.Comp.Kind == ModuleKind.Passive)
        {
            reason = ModuleBlockReason.MissingParts;
            return false;
        }

        if (!chassis.Comp.Active && (module.Comp.Allow & ModuleAllowFlags.ChassisInactive) == 0)
        {
            reason = ModuleBlockReason.ChassisInactive;
            return false;
        }

        if (!module.Comp.Enabled)
        {
            reason = ModuleBlockReason.MissingParts;
            return false;
        }

        if (_timing.CurTime < module.Comp.CooldownEnd)
        {
            reason = ModuleBlockReason.Cooldown;
            return false;
        }

        if (user != null
            && (module.Comp.Allow & ModuleAllowFlags.Incapacitated) == 0
            && !_actionBlocker.CanInteract(user.Value, null))
        {
            reason = ModuleBlockReason.Incapacitated;
            return false;
        }

        // Only a one-shot spend needs to be affordable up front; a toggle checks per second.
        var upfront = module.Comp.Kind is ModuleKind.Usable ? module.Comp.UseCost : 0f;
        if (upfront > 0f && !_power.HasCharge(chassis, upfront))
        {
            reason = ModuleBlockReason.NoPower;
            return false;
        }

        return true;
    }

    /// <summary>
    ///     What the player asked for by clicking a module, without them having to know
    ///     which kind it is: toggles flip, usables fire, active modules select.
    /// </summary>
    public bool TrySelect(Entity<ChassisModuleComponent> module, EntityUid? user)
    {
        if (module.Comp.Chassis is not { } chassisUid
            || !TryComp<ModularChassisComponent>(chassisUid, out var chassisComp))
            return false;

        var chassis = new Entity<ModularChassisComponent>(chassisUid, chassisComp);

        if (!CanUse(module, chassis, user, out var reason))
        {
            if (user != null)
            {
                _popup.PopupClient(GetBlockMessage(reason, module), module, user.Value);
                _audio.PlayPredicted(chassis.Comp.FailSound, module, user);
            }

            return false;
        }

        return module.Comp.Kind switch
        {
            ModuleKind.Usable => Use(module, chassis, user, null),
            _ => module.Comp.Active
                ? Deactivate(module, chassis, user)
                : Activate(module, chassis, user),
        };
    }

    /// <summary>
    ///     Switches a module on. Active modules deselect whatever was selected before.
    /// </summary>
    public bool Activate(Entity<ChassisModuleComponent> module, Entity<ModularChassisComponent> chassis, EntityUid? user)
    {
        if (module.Comp.Active)
            return false;

        var attempt = new ModuleActivateAttemptEvent(chassis, user, false);
        RaiseLocalEvent(module, ref attempt);
        if (attempt.Cancelled)
            return false;

        if (module.Comp.Kind == ModuleKind.Active)
        {
            // Only one active module at a time; stand the previous one down first.
            if (chassis.Comp.SelectedModule is { } previous
                && previous != module.Owner
                && TryComp<ChassisModuleComponent>(previous, out var previousComp))
            {
                Deactivate((previous, previousComp), chassis, user);
            }

            chassis.Comp.SelectedModule = module;
            Dirty(chassis);
        }

        module.Comp.Active = true;
        Dirty(module);

        var ev = new ModuleActivatedEvent(chassis, user);
        RaiseLocalEvent(module, ref ev);

        return true;
    }

    /// <summary>
    ///     Switches a module off. <paramref name="quiet"/> suppresses feedback for
    ///     shutdowns the player did not ask for.
    /// </summary>
    public bool Deactivate(
        Entity<ChassisModuleComponent> module,
        EntityUid chassis,
        EntityUid? user,
        bool quiet = false)
    {
        if (!module.Comp.Active)
            return false;

        module.Comp.Active = false;

        if (TryComp<ModularChassisComponent>(chassis, out var chassisComp)
            && chassisComp.SelectedModule == module.Owner)
        {
            chassisComp.SelectedModule = null;
            Dirty(chassis, chassisComp);
        }

        Dirty(module);

        var ev = new ModuleDeactivatedEvent(chassis, quiet ? null : user);
        RaiseLocalEvent(module, ref ev);

        return true;
    }

    /// <summary>
    ///     Fires a module's one-shot effect, spending charge and starting its cooldown.
    ///     Charge is only spent if a behaviour actually handled the use.
    /// </summary>
    public bool Use(
        Entity<ChassisModuleComponent> module,
        Entity<ModularChassisComponent> chassis,
        EntityUid? user,
        EntityUid? target)
    {
        var ev = new ModuleUsedEvent(chassis, user, target, false);
        RaiseLocalEvent(module, ref ev);

        if (!ev.Handled)
            return false;

        if (module.Comp.UseCost > 0f && !_power.TryUseCharge(chassis, module.Comp.UseCost))
            return false;

        StartCooldown(module);
        return true;
    }

    public void StartCooldown(Entity<ChassisModuleComponent> module, TimeSpan? duration = null)
    {
        var length = duration ?? module.Comp.Cooldown;
        if (length <= TimeSpan.Zero)
            return;

        module.Comp.CooldownEnd = _timing.CurTime + length;
        Dirty(module);
    }

    public TimeSpan GetCooldownRemaining(ChassisModuleComponent module)
    {
        var remaining = module.CooldownEnd - _timing.CurTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private string GetBlockMessage(ModuleBlockReason reason, Entity<ChassisModuleComponent> module)
    {
        return reason switch
        {
            ModuleBlockReason.NoPower => Loc.GetString("chassis-module-no-power"),
            ModuleBlockReason.Cooldown => Loc.GetString("chassis-module-cooldown"),
            ModuleBlockReason.MissingParts => Loc.GetString("chassis-module-missing-parts"),
            ModuleBlockReason.ChassisInactive => Loc.GetString("chassis-module-chassis-inactive"),
            ModuleBlockReason.NotWorn => Loc.GetString("chassis-module-not-worn"),
            ModuleBlockReason.Incapacitated => Loc.GetString("chassis-module-incapacitated"),
            ModuleBlockReason.Malfunctioning => Loc.GetString("chassis-module-malfunctioning"),
            _ => Loc.GetString("chassis-module-unavailable"),
        };
    }
}

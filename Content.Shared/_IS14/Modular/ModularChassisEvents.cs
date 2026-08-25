// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Inventory;

namespace Content.Shared._IS14.Modular;

#region Power

/// <summary>
///     Raised on a chassis to find out how much charge it has.
///     Whoever holds the charge — a MOD core, a mech battery — answers.
/// </summary>
[ByRefEvent]
public record struct ChassisGetChargeEvent(float Current, float Max, bool Handled)
{
    public float Current = Current;
    public float Max = Max;
    public bool Handled = Handled;
}

/// <summary>
///     Raised on a chassis to spend charge. Handlers must only set
///     <see cref="Handled"/> if they actually took the full amount.
/// </summary>
[ByRefEvent]
public record struct ChassisTryUseChargeEvent(float Amount, bool Handled)
{
    public readonly float Amount = Amount;
    public bool Handled = Handled;
}

/// <summary>
///     Raised on a chassis to put charge back in.
/// </summary>
[ByRefEvent]
public record struct ChassisAddChargeEvent(float Amount, bool Handled)
{
    public readonly float Amount = Amount;
    public bool Handled = Handled;
}

/// <summary>
///     Raised on a chassis after its charge changes enough to matter, so UI and
///     alerts can refresh without polling.
/// </summary>
[ByRefEvent]
public readonly record struct ChassisPowerChangedEvent(float Current, float Max);

/// <summary>
///     Raised on a chassis when it runs out of charge while active.
/// </summary>
[ByRefEvent]
public readonly record struct ChassisPowerDepletedEvent;

#endregion

#region Chassis lifecycle

/// <summary>
///     Raised on a chassis whenever the set of installed modules changes.
/// </summary>
[ByRefEvent]
public readonly record struct ChassisModulesChangedEvent;

/// <summary>
///     Raised on a chassis when it is switched on or off.
/// </summary>
[ByRefEvent]
public readonly record struct ChassisStateChangedEvent(bool Active);

/// <summary>
///     Raised on a chassis to collect which slots are currently available to modules.
///     The modsuit layer answers with the slots of every deployed and sealed part;
///     a mech would answer with whatever hardpoints it has.
///     A chassis that never answers is treated as providing every slot.
/// </summary>
[ByRefEvent]
public record struct ChassisGetAvailableSlotsEvent(SlotFlags Slots, bool Handled)
{
    public SlotFlags Slots = Slots;
    public bool Handled = Handled;
}

/// <summary>
///     Raised on a chassis to find out who is operating it — the wearer of a suit,
///     the pilot of a mech. Behaviours that affect a person go through this rather than
///     reaching for a wearer field the chassis layer does not have.
/// </summary>
[ByRefEvent]
public record struct ChassisGetUserEvent(EntityUid? User)
{
    public EntityUid? User = User;
}

/// <summary>
///     Raised on a chassis when its operator changes, so behaviours can move their
///     effects onto the new person instead of leaving them on the old one.
/// </summary>
[ByRefEvent]
public readonly record struct ChassisUserChangedEvent(EntityUid? User);

/// <summary>
///     Raised on a chassis when someone works a prying tool into its open panel.
///     Whoever owns the chassis' power source answers by handing it over; nothing else
///     comes out this way, because every other piece of hardware has a button for it.
///     <paramref name="DryRun"/> asks only whether there is anything to pry, so the
///     tool can refuse before the player stands there for two seconds.
/// </summary>
[ByRefEvent]
public record struct ChassisPryEvent(EntityUid User, bool DryRun, bool Handled)
{
    public readonly EntityUid User = User;
    public readonly bool DryRun = DryRun;
    public bool Handled = Handled;
}

/// <summary>
///     Raised on a chassis before a module is installed. Cancel to refuse.
/// </summary>
[ByRefEvent]
public record struct ChassisInstallModuleAttemptEvent(EntityUid Module, EntityUid? User, bool Cancelled)
{
    public readonly EntityUid Module = Module;
    public readonly EntityUid? User = User;
    public bool Cancelled = Cancelled;
}

#endregion

#region Module lifecycle

/// <summary>
///     Raised on a module once it has been installed into a chassis.
/// </summary>
[ByRefEvent]
public readonly record struct ModuleInstalledEvent(EntityUid Chassis);

/// <summary>
///     Raised on a module as it is removed from a chassis.
/// </summary>
[ByRefEvent]
public readonly record struct ModuleUninstalledEvent(EntityUid Chassis);

/// <summary>
///     Raised on a module when its slot requirements become satisfied and the chassis
///     can run it. Passive behaviours should apply their effect here.
/// </summary>
[ByRefEvent]
public readonly record struct ModuleEnabledEvent(EntityUid Chassis);

/// <summary>
///     Raised on a module when it can no longer run — a part was retracted, the chassis
///     switched off, power ran out. Passive behaviours should undo their effect here.
/// </summary>
[ByRefEvent]
public readonly record struct ModuleDisabledEvent(EntityUid Chassis);

/// <summary>
///     Raised on a module before it switches on. Cancel to refuse, optionally with a reason.
/// </summary>
[ByRefEvent]
public record struct ModuleActivateAttemptEvent(EntityUid Chassis, EntityUid? User, bool Cancelled)
{
    public readonly EntityUid Chassis = Chassis;
    public readonly EntityUid? User = User;
    public bool Cancelled = Cancelled;
}

/// <summary>
///     Raised on a module after it switches on.
/// </summary>
[ByRefEvent]
public readonly record struct ModuleActivatedEvent(EntityUid Chassis, EntityUid? User);

/// <summary>
///     Raised on a module after it switches off.
/// </summary>
[ByRefEvent]
public readonly record struct ModuleDeactivatedEvent(EntityUid Chassis, EntityUid? User);

/// <summary>
///     Raised on each installed module when the chassis' operator changes.
///     Behaviours re-point their effects at the new person from here.
///     A per-module relay rather than a second chassis subscription, because the engine
///     allows only one handler per (component, event) pair across the whole game.
/// </summary>
[ByRefEvent]
public readonly record struct ModuleUserChangedEvent(EntityUid Chassis, EntityUid? User);

/// <summary>
///     Raised on a module when it fires a one-shot effect.
///     <paramref name="Target"/> is set when triggered by a special click.
/// </summary>
[ByRefEvent]
public record struct ModuleUsedEvent(EntityUid Chassis, EntityUid? User, EntityUid? Target, bool Handled)
{
    public readonly EntityUid Chassis = Chassis;
    public readonly EntityUid? User = User;
    public readonly EntityUid? Target = Target;
    public bool Handled = Handled;
}

#endregion

#region UI

/// <summary>
///     Raised on a module to collect the settings it wants to expose in the UI.
/// </summary>
[ByRefEvent]
public record struct ModuleGetConfigEvent(List<ModuleConfigEntry> Entries)
{
    public readonly List<ModuleConfigEntry> Entries = Entries;
}

/// <summary>
///     Raised on a module when the player edits one of its settings.
/// </summary>
[ByRefEvent]
public record struct ModuleConfigChangedEvent(string Key, object? Value, bool Handled)
{
    public readonly string Key = Key;
    public readonly object? Value = Value;
    public bool Handled = Handled;
}

#endregion

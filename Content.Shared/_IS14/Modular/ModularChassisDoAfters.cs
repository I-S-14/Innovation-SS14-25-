// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modular;

/// <summary>
///     Screwdriver on the chassis: open or close the hardware panel.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ChassisTogglePanelDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Levering the chassis' power source out of its cradle. Modules are pulled from
///     the interface instead — a crowbar is for the one part that has to come out
///     when the interface is dead.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ChassisPryDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
///     Slotting a module into an open chassis.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ChassisInstallModuleDoAfterEvent : SimpleDoAfterEvent;

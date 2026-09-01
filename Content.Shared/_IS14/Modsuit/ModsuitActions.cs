// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular;
using Content.Shared.Actions;

namespace Content.Shared._IS14.Modsuit;

/// <summary>
///     Deploys every folded part, or retracts them all if they are already out.
/// </summary>
public sealed partial class ModsuitToggleDeployEvent : InstantActionEvent;

/// <summary>
///     Seals the suit up, or unseals it if it is already sealed.
/// </summary>
public sealed partial class ModsuitToggleSealEvent : InstantActionEvent;

/// <summary>
///     Opens the module radial menu.
/// </summary>
public sealed partial class ModsuitOpenModulesEvent : InstantActionEvent;

/// <summary>
///     Opens the suit's full readout.
///
///     Its own action rather than an option inside the ring: the ring is a gesture made
///     mid-task and dismissed, the readout is a window sat in front of, and burying one
///     behind the other made the slow thing cost a spin through the fast one.
/// </summary>
public sealed partial class ModsuitOpenPanelEvent : InstantActionEvent;

/// <summary>
///     Fires an <see cref="ModuleKind.Active"/> module's special click at a target.
/// </summary>
public sealed partial class ModsuitModuleTargetEvent : EntityTargetActionEvent;

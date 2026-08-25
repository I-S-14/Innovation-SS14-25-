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
///     Fires an <see cref="ModuleKind.Active"/> module's special click at a target.
/// </summary>
public sealed partial class ModsuitModuleTargetEvent : EntityTargetActionEvent;

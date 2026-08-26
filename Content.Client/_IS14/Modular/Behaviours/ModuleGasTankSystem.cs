// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Behaviours;

namespace Content.Client._IS14.Modular.Behaviours;

/// <summary>
///     The client half exists only so the shared behaviour has something to register.
///     Compressing gas needs an atmosphere, and the client does not have one.
/// </summary>
public sealed class ModuleGasTankSystem : SharedModuleGasTankSystem;

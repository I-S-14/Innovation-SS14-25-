// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Opens a module's own interface when it is switched on, and switches the module
///     back off when the window is closed. That pairing is the point: a readout the
///     player is not looking at should not be costing them power.
/// </summary>
public sealed class ModuleInterfaceSystem : ModuleBehaviourSystem<ModuleInterfaceComponent>
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedChassisModuleSystem _modules = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleInterfaceComponent, BoundUIClosedEvent>(OnUiClosed);
    }

    /// <summary>
    ///     A readout follows the switch, not mere installation.
    /// </summary>
    protected override bool RequiresActive(Entity<ModuleInterfaceComponent> ent) => true;

    protected override void Start(Entity<ModuleInterfaceComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Key is not { } key || GetChassisUser(chassis) is not { } user)
            return;

        _ui.TryOpenUi(ent.Owner, key, user);
    }

    protected override void Stop(Entity<ModuleInterfaceComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Key is { } key)
            _ui.CloseUi(ent.Owner, key);
    }

    /// <summary>
    ///     Closing the window is how the player says they are done, so that is what
    ///     stands the module down. Without this the suit would keep paying for a readout
    ///     nobody is reading.
    /// </summary>
    private void OnUiClosed(Entity<ModuleInterfaceComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.Key is not { } key || !args.UiKey.Equals(key))
            return;

        // Somebody else may still have it open.
        if (_ui.IsUiOpen(ent.Owner, key))
            return;

        if (!TryComp<ChassisModuleComponent>(ent, out var module)
            || module.Chassis is not { } chassis
            || !module.Active)
            return;

        _modules.Deactivate((ent.Owner, module), chassis, null, quiet: true);
    }
}

// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Shared plumbing for module behaviours: working out who the module should affect
///     and when a behaviour should be considered running.
/// </summary>
public abstract class ModuleBehaviourSystem<TComp> : EntitySystem where TComp : IComponent, new()
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TComp, ModuleEnabledEvent>(OnModuleEnabled);
        SubscribeLocalEvent<TComp, ModuleDisabledEvent>(OnModuleDisabled);
        SubscribeLocalEvent<TComp, ModuleActivatedEvent>(OnModuleActivated);
        SubscribeLocalEvent<TComp, ModuleDeactivatedEvent>(OnModuleDeactivated);
        SubscribeLocalEvent<TComp, ModuleUninstalledEvent>(OnModuleUninstalled);
        SubscribeLocalEvent<TComp, ModuleUserChangedEvent>(OnModuleUserChanged);
    }

    /// <summary>
    ///     Called when the behaviour should start doing its thing.
    /// </summary>
    protected abstract void Start(Entity<TComp> ent, EntityUid chassis);

    /// <summary>
    ///     Called when the behaviour should stop. Must be safe to call when it never started.
    /// </summary>
    protected abstract void Stop(Entity<TComp> ent, EntityUid chassis);

    /// <summary>
    ///     Whether this behaviour follows the module's on/off switch rather than merely
    ///     being installed and supplied.
    /// </summary>
    protected virtual bool RequiresActive(Entity<TComp> ent) => false;

    private void OnModuleEnabled(Entity<TComp> ent, ref ModuleEnabledEvent args)
    {
        if (!RequiresActive(ent))
            Start(ent, args.Chassis);
    }

    private void OnModuleDisabled(Entity<TComp> ent, ref ModuleDisabledEvent args)
    {
        // Always stop, even for active-gated behaviours: losing the required parts
        // has to tear the effect down regardless of the switch position.
        Stop(ent, args.Chassis);
    }

    private void OnModuleActivated(Entity<TComp> ent, ref ModuleActivatedEvent args)
    {
        if (RequiresActive(ent))
            Start(ent, args.Chassis);
    }

    private void OnModuleDeactivated(Entity<TComp> ent, ref ModuleDeactivatedEvent args)
    {
        if (RequiresActive(ent))
            Stop(ent, args.Chassis);
    }

    private void OnModuleUninstalled(Entity<TComp> ent, ref ModuleUninstalledEvent args)
    {
        Stop(ent, args.Chassis);
    }

    /// <summary>
    ///     Called when the chassis changed hands. Behaviours that put something on the
    ///     operator override this to move it; the rest do not care.
    /// </summary>
    protected virtual void UserChanged(Entity<TComp> ent, EntityUid chassis, EntityUid? user)
    {
    }

    private void OnModuleUserChanged(Entity<TComp> ent, ref ModuleUserChangedEvent args)
    {
        UserChanged(ent, args.Chassis, args.User);
    }

    /// <summary>
    ///     The person operating the chassis — a suit's wearer, a mech's pilot.
    /// </summary>
    protected EntityUid? GetChassisUser(EntityUid chassis)
    {
        var ev = new ChassisGetUserEvent(null);
        RaiseLocalEvent(chassis, ref ev);
        return ev.User;
    }

    /// <summary>
    ///     Resolves a grant target to a concrete entity, or null when there is nobody
    ///     to apply it to yet.
    /// </summary>
    protected EntityUid? ResolveTarget(EntityUid module, EntityUid chassis, ModuleGrantTarget target)
    {
        return target switch
        {
            ModuleGrantTarget.Wearer => GetChassisUser(chassis),
            ModuleGrantTarget.Chassis => chassis,
            ModuleGrantTarget.Self => module,
            _ => null,
        };
    }

    /// <summary>
    ///     The chassis a module is installed in, if any.
    /// </summary>
    protected EntityUid? GetChassis(EntityUid module)
    {
        return TryComp<ChassisModuleComponent>(module, out var comp) ? comp.Chassis : null;
    }
}

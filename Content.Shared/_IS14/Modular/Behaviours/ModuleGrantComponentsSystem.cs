// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Applies and removes a module's granted components.
///     This is the workhorse of the module catalogue — most modules do nothing but this.
/// </summary>
public sealed class ModuleGrantComponentsSystem : ModuleBehaviourSystem<ModuleGrantComponentsComponent>
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    /// <summary>
    ///     A wearer change has to move the grant with it, or the effect would stay stuck
    ///     on whoever took the suit off.
    /// </summary>
    protected override void UserChanged(Entity<ModuleGrantComponentsComponent> ent, EntityUid chassis, EntityUid? user)
    {
        if (ent.Comp.Target != ModuleGrantTarget.Wearer || ent.Comp.GrantedTo == null)
            return;

        if (user == null)
            Revoke(ent);
        else
            Start(ent, chassis);
    }

    protected override bool RequiresActive(Entity<ModuleGrantComponentsComponent> ent)
    {
        return ent.Comp.RequireActive;
    }

    protected override void Start(Entity<ModuleGrantComponentsComponent> ent, EntityUid chassis)
    {
        if (ResolveTarget(ent, chassis, ent.Comp.Target) is not { } target)
            return;

        // Re-granting to the same target would stack duplicate components.
        if (ent.Comp.GrantedTo == target)
            return;

        if (ent.Comp.GrantedTo != null)
            Revoke(ent);

        foreach (var (name, entry) in ent.Comp.Components)
        {
            var registration = _factory.GetRegistration(name);

            // Leave anything the target already has: overwriting it would silently
            // downgrade the wearer's own equipment, and revoking would then delete it.
            if (EntityManager.HasComponent(target, registration.Type))
                continue;

            var component = (IComponent)_factory.GetComponent(registration);
            var temp = (object)component;
            _serialization.CopyTo(entry.Component, ref temp);

            EntityManager.AddComponent(target, (Component)temp!);
            ent.Comp.GrantedKeys.Add(name);
        }

        ent.Comp.GrantedTo = target;
    }

    protected override void Stop(Entity<ModuleGrantComponentsComponent> ent, EntityUid chassis)
    {
        Revoke(ent);
    }

    private void Revoke(Entity<ModuleGrantComponentsComponent> ent)
    {
        if (ent.Comp.GrantedTo is not { } target)
            return;

        // The target may have been deleted out from under us — gibbed, deleted by admin.
        if (!TerminatingOrDeleted(target))
        {
            foreach (var name in ent.Comp.GrantedKeys)
            {
                EntityManager.RemoveComponent(target, _factory.GetRegistration(name).Type);
            }
        }

        ent.Comp.GrantedKeys.Clear();
        ent.Comp.GrantedTo = null;
    }
}

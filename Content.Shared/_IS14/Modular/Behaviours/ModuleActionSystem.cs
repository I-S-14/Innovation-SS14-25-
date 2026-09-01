// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Actions;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Hands the operator an action for as long as the module runs.
/// </summary>
public sealed class ModuleActionSystem : ModuleBehaviourSystem<ModuleActionComponent>
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    /// <summary>
    ///     Actions follow the operator: taken off the old one, handed to the new.
    /// </summary>
    protected override void UserChanged(Entity<ModuleActionComponent> ent, EntityUid chassis, EntityUid? user)
    {
        if (ent.Comp.GrantedTo == null)
            return;

        Revoke(ent);

        if (user != null)
            Start(ent, chassis);
    }

    protected override bool RequiresActive(Entity<ModuleActionComponent> ent)
    {
        return ent.Comp.RequireActive;
    }

    protected override void Start(Entity<ModuleActionComponent> ent, EntityUid chassis)
    {
        if (GetChassisUser(chassis) is not { } user)
            return;

        if (ent.Comp.GrantedTo == user)
            return;

        if (ent.Comp.GrantedTo != null)
            Revoke(ent);

        // The module owns the action entity so it survives being handed between people.
        _actions.AddAction(user, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
        ent.Comp.GrantedTo = user;
        Dirty(ent);
    }

    protected override void Stop(Entity<ModuleActionComponent> ent, EntityUid chassis)
    {
        Revoke(ent);
    }

    private void Revoke(Entity<ModuleActionComponent> ent)
    {
        if (ent.Comp.GrantedTo is not { } user)
            return;

        if (!TerminatingOrDeleted(user))
            _actions.RemoveAction(user, ent.Comp.ActionEntity);

        ent.Comp.GrantedTo = null;
        Dirty(ent);
    }
}

using Content.Shared._IS.Pet.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;

namespace Content.Shared._IS.Pet;

public abstract class SharedPetSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PetOverlordComponent, ComponentStartup>(OnOverlordStartup);
        SubscribeLocalEvent<PetOverlordComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PetComponent, ComponentShutdown>(OnPetShutdown);
        SubscribeLocalEvent<PetOverlordComponent, PetOrderActionEvent>(OnOrderAction);
    }

    private void OnOverlordStartup(Entity<PetOverlordComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out ActionsComponent? comp))
            return;

        _action.AddAction(ent.Owner, ref ent.Comp.StayActionEntity, ent.Comp.StayAction, component: comp);
        _action.AddAction(ent.Owner, ref ent.Comp.AttackActionEntity, ent.Comp.AttackAction, component: comp);
        _action.AddAction(ent.Owner, ref ent.Comp.FollowActionEntity, ent.Comp.FollowAction, component: comp);

        UpdateActions(ent, ent.Comp);
    }

    private void OnShutdown(Entity<PetOverlordComponent> ent, ref ComponentShutdown args)
    {
        foreach (var pet in ent.Comp.Pets)
        {
            if (TryComp(pet, out PetComponent? petComp))
                petComp.Overlord = null;
        }

        if (!TryComp(ent, out ActionsComponent? comp))
            return;

        var actions = new Entity<ActionsComponent?>(ent.Owner, comp);

        _action.RemoveAction(actions, ent.Comp.StayActionEntity);
        _action.RemoveAction(actions, ent.Comp.AttackActionEntity);
        _action.RemoveAction(actions, ent.Comp.FollowActionEntity);
    }

    public virtual void UpdateActions(EntityUid uid, PetOverlordComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _action.SetToggled(component.StayActionEntity, component.CurrentOrder == PetOrderType.Stay);
        _action.SetToggled(component.AttackActionEntity, component.CurrentOrder == PetOrderType.Attack);
        _action.SetToggled(component.FollowActionEntity, component.CurrentOrder == PetOrderType.Follow);
        _action.StartUseDelay(component.StayActionEntity);
        _action.StartUseDelay(component.AttackActionEntity);
        _action.StartUseDelay(component.FollowActionEntity);
    }

    private void OnOrderAction(Entity<PetOverlordComponent> ent, ref PetOrderActionEvent args)
    {
        if (ent.Comp.CurrentOrder == args.Type)
            return;
        args.Handled = true;

        ent.Comp.CurrentOrder = args.Type;
        Dirty(ent.Owner, ent.Comp);

        UpdateActions(ent.Owner, ent.Comp);
        UpdateAllPets(ent.Owner, ent.Comp);
    }

    public void UpdateAllPets(EntityUid uid, PetOverlordComponent component)
    {
        foreach (var pet in component.Pets)
        {
            if (!TryComp<PetComponent>(pet, out var pComp))
                continue;

            if (pComp.CurrentOrder == component.CurrentOrder)
                continue;

            UpdatePet((pet, pComp), component.CurrentOrder);
        }
    }

    public virtual void UpdatePet(Entity<PetComponent> ent, PetOrderType order) { }

    private void OnPetShutdown(Entity<PetComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Overlord != null)
        {
            if (TryComp(ent.Comp.Overlord.Value, out PetOverlordComponent? poComp))
                poComp.Pets.Remove(ent.Owner);
        }
    }
}

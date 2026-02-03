
using System.Diagnostics.Tracing;
using Content.Shared._Innovation.Pets;
using Content.Shared._Innovation.Pets.PetCommands;
using Robust.Shared._Innovation.Pets.Components;

namespace Robust.Shared._Innovation.Pets.Systems;

public abstract class SharedPetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
    }

    public void MakePet(EntityUid uid, EntityUid owner, Dictionary<string, PetCommand> commands)
    {
        EnsureComp<PetComponent>(uid);
        SetOwner(uid, owner);
        SetCommands(uid, commands);
    }


    public void SetOwner(EntityUid uid, EntityUid owner)
    {
        if (!TryComp<PetComponent>(uid, out var petComponent)) return;
        petComponent.PetOwner = owner;
    }

    public void SetCurrentOrder(EntityUid uid, PetOrderType orderType, EntityUid? target = null)
    {
        if (!TryComp<PetComponent>(uid, out var petComponent)) return;
        petComponent.CurrentOrderType = orderType;
        RaiseLocalEvent(uid, new PetUpdateHtnTask(target));
    }

    private void SetCommands(EntityUid uid, Dictionary<string, PetCommand> commands)
    {
        if (!TryComp<PetComponent>(uid, out var petComponent)) return;
        petComponent.Commands = commands;
    }

    public bool SetCurrentCommand(EntityUid uid, string command)
    {
        if (!TryComp<PetComponent>(uid, out var petComponent) || !petComponent.Commands.ContainsKey(command)) return false;
        petComponent.CurrentCommand = command;
        return true;
    }
}

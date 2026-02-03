
using Content.Shared._Innovation.Pets.PetCommands;
using Robust.Shared._Innovation.Pets.Components;
using Robust.Shared._Innovation.Pets.Systems;

public sealed partial class PetAttackCommand : PetCommand
{
    public override void Execute(PetBaseArgs args)
    {
        args.EntityManager.System<SharedPetSystem>().SetCurrentOrder(args.PetEntity, PetOrderType.Attack);
    }
}

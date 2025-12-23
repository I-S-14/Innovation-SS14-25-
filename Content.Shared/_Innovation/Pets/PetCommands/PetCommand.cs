
using JetBrains.Annotations;

namespace Content.Shared._Innovation.Pets.PetCommands;

public abstract partial class PetCommand
{
    public abstract void Execute(PetBaseArgs args);
}

public record class PetBaseArgs
{
    public EntityUid PetEntity;

    public IEntityManager EntityManager = default!;

    public PetBaseArgs(EntityUid petEntity, IEntityManager entityManager)
    {
        PetEntity = petEntity;
        EntityManager = entityManager;
    }
}

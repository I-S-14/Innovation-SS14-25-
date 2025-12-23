using Robust.Shared.Serialization;

namespace Content.Shared._Innovation.Pets;

public sealed class PetUpdateHtnTask : EntityEventArgs
{
    public EntityUid? Target;

    public PetUpdateHtnTask(EntityUid? target)
    {
        Target = target;
    }
}

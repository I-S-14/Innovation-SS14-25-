using Robust.Server._Innovation.Pets.Systems;
using Robust.Shared._Innovation.Pets;
using Robust.Shared.Prototypes;

namespace Robust.Server._Innovation.Pets.Components;

[RegisterComponent, Access(typeof(PetSystem))]
public sealed partial class PetOwnerComponent : Component
{
    [DataField, ViewVariables]
    public List<ProtoId<PetPrototype>> Pets = new();
}

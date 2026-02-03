
using Content.Shared._Innovation.Pets.PetCommands;
using Robust.Shared._Innovation.Pets.Systems;
using Robust.Shared.Serialization;

namespace Robust.Shared._Innovation.Pets.Components;

[RegisterComponent, Access(typeof(SharedPetSystem))]
public sealed partial class PetComponent : Component
{
    [ViewVariables]
    public EntityUid? PetOwner;

    /// <summary>
    /// Command dictionary
    /// </summary>
    [ViewVariables]
    public Dictionary<string, PetCommand> Commands { get; set; } = new();

    public string CurrentCommand;

    public PetOrderType CurrentOrderType = PetOrderType.Loose;
}

[Serializable, NetSerializable]
public enum PetOrderType : byte
{
    Stay,
    Follow,
    Attack,
    Loose
}

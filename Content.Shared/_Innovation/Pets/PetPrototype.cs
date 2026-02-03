using Robust.Shared.Prototypes;
using Content.Shared._Innovation.Pets.PetCommands;

namespace Robust.Shared._Innovation.Pets;

/// <summary>
/// Prototype for Pet
/// </summary>
[Prototype]
public sealed partial class PetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Entity prototype of a pet.
    /// </summary>
    [DataField(required: true)]
    public string Pet = string.Empty;

    /// <summary>
    /// Commands that pet will respond to.
    /// </summary>
    [DataField]
    public Dictionary<string, PetCommand> Commands = new();
}

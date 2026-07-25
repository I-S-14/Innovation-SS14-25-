using Content.Shared._IS.Pet.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS.Pet;

[Prototype]
public sealed partial class PetOrderPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? Language { get; private set; }

    [DataField]
    public string Message { get; private set; } = default!;

    [DataField]
    public PetOrderType? Order { get; private set; }

    [DataField]
    public string? SayOnOrder { get; private set; } = default!;
}

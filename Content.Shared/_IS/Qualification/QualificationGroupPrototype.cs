using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS.Qualification;

[Prototype]
public sealed partial class QualificationGroupPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> JobPrototypes = default!;

    [DataField(required: true)]
    public HashSet<ProtoId<QualificationPrototype>> QualificationHashSet = default!;
}
